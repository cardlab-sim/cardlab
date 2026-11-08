using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Controls;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class CardholderViewEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public CardholderViewEndpointTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-1 · PR-06 · integration
    // Given a reset and Ops van purchases £120.00 and £30.50 approved and £500.00 declined (simulate endpoint), When GET /api/cardholder/cards/{id}, Then 200 with card {name "Ops van", lastFour "0001", cardholderName "Priya Nair"}, balanceOwed "150.50", availability "available", account {displayName "Brightline Supplies Ltd current account", lastFour "7731", availableBalance "25000.00"}; every seeded card's view loads; an unknown id → 404
    // Catches: a wrong balance or account on the cardholder view (AC-1, AC-4, R-2, R-16)
    [Fact]
    public async Task View_shows_balance_cardholder_and_account()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("120.00", "fuel", "Shell", null));
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("30.50", "travel", "Rail Co", null));
        await _fixture.Database.SetStatusAsync(TestSeed.OpsVan.Name, CardStatus.Frozen);
        var declined = await client.SimulatePurchaseAsync(id, new SimulatedPurchase("500.00", "other", "Depot", null));
        await _fixture.Database.SetStatusAsync(TestSeed.OpsVan.Name, CardStatus.Active);
        Assert.Equal("declined", declined.Body!.Outcome);

        var view = await client.GetCardholderViewAsync(id);

        Assert.Equal(HttpStatusCode.OK, view.Status);
        var body = view.Body!;
        Assert.Equal("Ops van", body.Card.Name);
        Assert.Equal("0001", body.Card.LastFour);
        Assert.Equal("Priya Nair", body.Card.CardholderName);
        Assert.Equal("150.50", body.BalanceOwed);
        Assert.Equal("available", body.Availability);
        Assert.Equal("Brightline Supplies Ltd current account", body.Account.DisplayName);
        Assert.Equal("7731", body.Account.LastFour);
        Assert.Equal("25000.00", body.Account.AvailableBalance);
        foreach (var card in TestSeed.Cards)
        {
            var cardId = (await _fixture.Database.CardIdAsync(card.Name)).Value;
            var seeded = await client.GetCardholderViewAsync(cardId);
            Assert.Equal(HttpStatusCode.OK, seeded.Status);
            Assert.Equal(card.CardholderName, seeded.Body!.Card.CardholderName);
        }

        var unknown = await client.GetCardholderViewAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"));
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
    }
}
