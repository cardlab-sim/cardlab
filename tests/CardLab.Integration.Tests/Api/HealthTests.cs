using System.Diagnostics;
using System.Net;
using CardLab.Api.Endpoints;
using CardLab.Infrastructure;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    // T-I-10 · PR-09 · integration
    // Given the host with CardLab:Commit "test-commit", When GET /api/health, Then 200 {commit:"test-commit", database:"ok"} within 60 s
    // Catches: health not reporting the commit the deploy waits for (AC-34, AC-27)
    [Fact]
    public async Task Health_reports_commit_and_database_ok()
    {
        var api = _factory.CreateApiClient();
        var clock = Stopwatch.StartNew();

        var response = await api.HealthAsync();

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("test-commit", response.Body?.Commit);
        Assert.Equal("ok", response.Body?.Database);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60));
    }

    // T-I-10 · PR-09 review B-1
    // Given CardLab:Commit set, empty or blank and an informational version such as 1.0.0+<sha>, When the commit is resolved, Then config wins, else the part after '+', else the version, else "unknown"
    // Catches: the deploy health wait (commit == SHA) never matching on demo, where CardLab:Commit is unset (AC-27, AC-34)
    [Theory]
    [InlineData("test-commit", "1.0.0+abc", "test-commit")]
    [InlineData("", "1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("  ", "1.0.0", "1.0.0")]
    [InlineData("", null, "unknown")]
    public void Commit_comes_from_config_then_source_revision(string configured, string? informationalVersion, string expected) =>
        Assert.Equal(expected, BuildInfo.Resolve(configured, informationalVersion));

    // T-I-10 · PR-09 · integration
    // Given a DatabaseProbe on a connection string to a non-existent server (constructed directly, short connect timeout), When CanConnectAsync, Then false without throwing; and the handler maps false to 503 {database:"unreachable"}
    // Catches: health endpoint throwing 500 when the database is down (design §9)
    [Fact]
    public async Task Health_reports_unreachable_database_as_503()
    {
        var unreachable = new SqlConnectionStringBuilder("Server=tcp:127.0.0.1,1;Database=CardLab;User Id=x;Password=x;Connect Timeout=2;Encrypt=False");
        await using var db = new CardLabDbContext(new DbContextOptionsBuilder<CardLabDbContext>().UseSqlServer(unreachable.ConnectionString).Options);

        Assert.False(await new DatabaseProbe(db).CanConnectAsync(TestContext.Current.CancellationToken));

        // The same failure seen through the host: take the database offline under a running API.
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var api = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await api.HealthAsync()).Status);
        var master = new SqlConnectionStringBuilder(factory.Database.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        var name = new SqlConnectionStringBuilder(factory.Database.ConnectionString).InitialCatalog;
        await SetStateAsync(master, name, "OFFLINE WITH ROLLBACK IMMEDIATE");
        try
        {
            var down = await api.HealthAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, down.Status);
            Assert.Equal("test-commit", down.Body?.Commit);
            Assert.Equal("unreachable", down.Body?.Database);
        }
        finally
        {
            await SetStateAsync(master, name, "ONLINE");
        }
    }

    private static async Task SetStateAsync(string masterConnection, string database, string state)
    {
        await using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{database}] SET {state}";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
