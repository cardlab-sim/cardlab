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

        var services = new ServiceCollection();
        services.AddLogging();
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
        await using var provider = services.BuildServiceProvider();

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
