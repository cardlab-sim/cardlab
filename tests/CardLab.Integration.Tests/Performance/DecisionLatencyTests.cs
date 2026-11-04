using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Performance;

[Collection("sql")]
public sealed class DecisionLatencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DecisionLatencyTests(ApiFactory factory) => _factory = factory;

    // T-I-14 · PR-08 · integration (performance)
    // Given Ops van with 1,000 stored transactions spread over the last 40 days and limits in fuel, travel and other (day, week, month), When 200 sequential POST /api/authorisations across those categories are timed after 10 warm-up requests, Then the 95th percentile is at most 300 ms
    // Catches: unindexed spend sums or N+1 queries on the hot path (AC-35, NFR-5)
    [Fact(Skip = "todo")]
    public void P95_decision_latency_is_within_300_ms()
    {
    }
}
