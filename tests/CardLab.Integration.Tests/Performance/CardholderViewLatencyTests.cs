using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Performance;

[Collection("sql")]
public sealed class CardholderViewLatencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public CardholderViewLatencyTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-17 · PR-06 · integration
    // Given Ops van with 1,000 transactions (SeedTransactionsAsync) and 200 payments in mixed states, When GET /api/cardholder/cards/{id} 50 times after 5 warm-up calls, Then p95 ≤ 300 ms
    // Catches: unindexed balance or activity queries (NFR-2, design §13)
    [Fact(Skip = "todo")]
    public void Cardholder_view_p95_within_300_ms()
    {
    }
}
