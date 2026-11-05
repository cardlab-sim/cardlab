using System.Diagnostics;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Lookup;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Seed;

[Collection("sql")]
public sealed class StartupRetryTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public StartupRetryTests(SqlTestDatabase database) => _database = database;

    // T-I-22 · PR-12 · integration
    // Given the test database OFFLINE, When InitialiseDatabaseAsync starts and the database goes ONLINE after 10 s, Then it completes without exception within 60 s
    // Catches: crash loop on database resume after the free tier's idle auto-pause (BUG-008, review F-1)
    [Fact]
    public async Task Initialisation_waits_for_a_database_that_comes_back_online()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(_database.ConnectionString);
        var databaseName = connection.InitialCatalog;
        connection.InitialCatalog = "master";
        var masterConnectionString = connection.ConnectionString;

        await using var provider = BuildProvider(logs: null);

        await SetStateAsync(masterConnectionString, databaseName, "OFFLINE WITH ROLLBACK IMMEDIATE", ct);
        try
        {
            var clock = Stopwatch.StartNew();
            var initialisation = provider.InitialiseDatabaseAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            Assert.False(initialisation.IsCompleted, "initialisation finished while the database was offline");
            await SetStateAsync(masterConnectionString, databaseName, "ONLINE", ct);

            await initialisation.WaitAsync(TimeSpan.FromSeconds(50), ct);

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"took {clock.Elapsed}");
        }
        finally
        {
            await SetStateAsync(masterConnectionString, databaseName, "ONLINE", CancellationToken.None);
        }
    }

    // T-I-22 · PR-12 · integration
    // Given the test database stays OFFLINE, When initialisation runs with a 3 s window and 1 s delay, Then it throws the SqlException within 20 s, logs at least two DatabaseNotReady warnings and none mentions the database name
    // Catches: infinite retry hiding a real failure (design §9), non-SqlException retried, missing per-attempt log, connection details in the log
    [Fact]
    public async Task Initialisation_gives_up_after_the_retry_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = new SqlConnectionStringBuilder(_database.ConnectionString);
        var databaseName = connection.InitialCatalog;
        connection.InitialCatalog = "master";
        var masterConnectionString = connection.ConnectionString;
        var logs = new InMemoryLogExporter();
        await using var provider = BuildProvider(logs);

        await SetStateAsync(masterConnectionString, databaseName, "OFFLINE WITH ROLLBACK IMMEDIATE", ct);
        try
        {
            await Assert.ThrowsAsync<SqlException>(() =>
                DatabaseSetup.InitialiseDatabaseAsync(provider, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1), ct).WaitAsync(TimeSpan.FromSeconds(20), ct));

            var attempts = logs.All("DatabaseNotReady");
            Assert.True(attempts.Count >= 2, $"expected at least 2 DatabaseNotReady records, got {attempts.Count}");
            Assert.All(attempts, r => Assert.DoesNotContain(databaseName, r.FormattedMessage ?? "", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await SetStateAsync(masterConnectionString, databaseName, "ONLINE", CancellationToken.None);
        }
    }

    private ServiceProvider BuildProvider(InMemoryLogExporter? logs)
    {
        var services = new ServiceCollection();
        if (logs is null)
        {
            services.AddLogging();
        }
        else
        {
            services.AddLogging(b => b.AddOpenTelemetry(o => o.AddProcessor(new SimpleLogRecordExportProcessor(logs))));
        }

        services.AddDbContext<CardLabDbContext>(o => o.UseSqlServer(_database.ConnectionString));
        services.Configure<LookupOptions>(o => o.LookupKey = TestSeed.DevLookupKey);
        services.Configure<SeedOptions>(o =>
        {
            o.AccountName = TestSeed.AccountName;
            o.TimeZoneId = TestSeed.TimeZoneId;
            o.AdminId = TestSeed.AdminId;
            o.AdminName = TestSeed.AdminName;
            o.Cards = TestSeed.Cards.Select(c => new SeedCardOptions { Name = c.Name, CardholderName = c.CardholderName, CardNumber = c.CardNumber }).ToList();
        });
        services.AddSingleton<ICardNumberLookup, HmacCardNumberLookup>();
        return services.BuildServiceProvider();
    }

    private static async Task SetStateAsync(string masterConnectionString, string databaseName, string state, CancellationToken ct)
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{databaseName}] SET {state}";
        await command.ExecuteNonQueryAsync(ct);
    }
}
