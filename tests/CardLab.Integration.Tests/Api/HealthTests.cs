using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    // T-I-10 · PR-09 · integration
    // Given the host with CardLab:Commit "test-commit", When GET /api/health, Then 200 {commit:"test-commit", database:"ok"} within 60 s
    // Catches: health not reporting the commit the deploy waits for (AC-34, AC-27)
    [Fact(Skip = "todo")]
    public void Health_reports_commit_and_database_ok()
    {
    }

    // T-I-10 · PR-09 · integration
    // Given a DatabaseProbe on a connection string to a non-existent server (constructed directly, short connect timeout), When CanConnectAsync, Then false without throwing; and the handler maps false to 503 {database:"unreachable"}
    // Catches: health endpoint throwing 500 when the database is down (design §9)
    [Fact(Skip = "todo")]
    public void Health_reports_unreachable_database_as_503()
    {
    }
}
