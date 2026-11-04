using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class TestEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TestEndpointsTests(ApiFactory factory) => _factory = factory;

    // T-I-15 · PR-09 · integration
    // Given a frozen card with a limit, a transaction and audit events, When POST /api/test/reset, Then 204 and every card is active with no limits, transactions or audit events; seeded cards still exist
    // Catches: reset leaving state behind, breaking E2E isolation (DEC-16)
    [Fact(Skip = "todo")]
    public void Reset_clears_state_when_enabled()
    {
    }
}

[Collection("sql")]
public sealed class TestEndpointsDisabledTests : IClassFixture<ApiFactoryWithoutTestEndpoints>
{
    private readonly ApiFactoryWithoutTestEndpoints _factory;

    public TestEndpointsDisabledTests(ApiFactoryWithoutTestEndpoints factory) => _factory = factory;

    // T-I-15 · PR-09 · integration
    // Given CardLab:TestEndpoints=false, When POST /api/test/reset, Then 404 and nothing changed
    // Catches: the reset endpoint reachable on demo (DEC-16)
    [Fact(Skip = "todo")]
    public void Reset_is_404_when_disabled()
    {
    }
}
