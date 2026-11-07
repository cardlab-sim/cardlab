using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class CardholderViewEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public CardholderViewEndpointTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-1 · PR-06 · integration
    // Given a reset and Ops van purchases £120.00 and £30.50 approved and £500.00 declined (simulate endpoint), When GET /api/cardholder/cards/{id}, Then 200 with card {name "Ops van", lastFour "0001", cardholderName "Priya Nair"}, balanceOwed "150.50", availability "available", account {displayName "Brightline Supplies Ltd current account", lastFour "7731", availableBalance "25000.00"}; every seeded card's view loads; an unknown id → 404
    // Catches: a wrong balance or account on the cardholder view (AC-1, AC-4, R-2, R-16)
    [Fact(Skip = "todo")]
    public void View_shows_balance_cardholder_and_account()
    {
    }
}
