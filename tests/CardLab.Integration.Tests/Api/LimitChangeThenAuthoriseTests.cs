using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class LimitChangeThenAuthoriseTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LimitChangeThenAuthoriseTests(ApiFactory factory) => _factory = factory;

    // T-I-8 · PR-11 · integration
    // Given Ops van with PUT fuel/day "50.00" and £40.00 approved fuel today (via POST /api/authorisations), When PUT fuel/day "30.00" then a £0.01 fuel authorisation today, Then the limit is "30.00", the two earlier transactions are unchanged in GET transactions, and the response is declined limit_exceeded with remaining "0.00"
    // Catches: lowering below spend rejected or rewriting history (AC-30, R-13)
    [Fact(Skip = "todo")]
    public void Lowering_a_limit_below_spend_declines_further_spend()
    {
    }

    // T-I-8 · PR-11 · integration
    // Given the card of the previous test state (limit 30.00, £40.00 spent), When DELETE fuel/day then a £1,000.00 fuel authorisation today, Then approved with limits []
    // Catches: removed limit still enforced (cache) (AC-31)
    [Fact(Skip = "todo")]
    public void Removing_a_limit_makes_the_category_unlimited_at_once()
    {
    }
}
