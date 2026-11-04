using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Store;

[Collection("sql")]
public sealed class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConcurrencyTests(ApiFactory factory) => _factory = factory;

    // T-I-6 · PR-08 · integration
    // Given Ops van Fuel/Day £50.00 and no spend today, When 10 POST /api/authorisations for £10.00 fuel with the same timestamp are sent in parallel (Task.WhenAll, separate HttpClients), Then exactly 5 approved and 5 declined limit_exceeded, and the approved Fuel spend for the day in the database is £50.00
    // Catches: missing or ineffective per-card lock (AC-21, R-12, DEC-4)
    [Fact(Skip = "todo")]
    public void Ten_parallel_requests_approve_exactly_five()
    {
    }
}
