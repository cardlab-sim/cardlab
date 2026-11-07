using System.Net;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class TestEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TestEndpointsTests(ApiFactory factory) => _factory = factory;

    // T-I-15 · PR-09 · integration
    // Given a frozen card with a limit, a transaction and audit events, When POST /api/test/reset, Then 204 and every card is active with no limits, transactions or audit events; seeded cards still exist
    // Catches: reset leaving state behind, breaking E2E isolation (DEC-16)
    [Fact]
    public async Task Reset_clears_state_when_enabled()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Ops van")).Value;
        await api.FreezeAsync(id);
        await api.SetLimitAsync(id, "fuel", "day", "50.00");
        await _factory.Database.SeedLimitAsync("Marketing", Category.Travel, Period.Week, 20m);
        await _factory.Database.SetStatusAsync("Marketing", CardStatus.Frozen);
        await _factory.Database.SeedTransactionsAsync("Ops van", [new TransactionBuilder().Build()]);
        Assert.True(await _factory.Database.CountAuditEventsAsync() > 0);
        Assert.True(await _factory.Database.CountTransactionsAsync() > 0);

        var reset = await api.ResetAsync();

        Assert.Equal(HttpStatusCode.NoContent, reset.Status);
        Assert.Equal(0, await _factory.Database.CountAuditEventsAsync());
        Assert.Equal(0, await _factory.Database.CountTransactionsAsync());
        var cards = (await api.ListCardsAsync()).Body!;
        Assert.Equal(["Field sales", "Marketing", "Ops van"], cards.Select(c => c.Name).ToArray());
        Assert.All(cards, c => Assert.Equal("active", c.Status));
        foreach (var card in cards)
        {
            Assert.Empty((await api.GetCardAsync(card.Id)).Body!.Limits);
        }
    }

    // T-I-11 · PR-05 · integration
    // Given completed and pending payments, quotes, events, bank entries and steering, When POST /api/test/reset, Then 204; PaymentQuote, Payment, PaymentEvent, BankDebitRequest, BankEntry and BankSteering are empty; BankAccount is back at £25,000.00; LinkedAccount still exists; every seeded card's balance (GetBalanceAsync) is £0.00
    // Catches: state leaking between E2E journeys (AC-21, R-15)
    [Fact(Skip = "todo")]
    public void Reset_clears_payments_and_reseeds_the_bank()
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
    [Fact]
    public async Task Reset_is_404_when_disabled()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Ops van")).Value;
        await api.FreezeAsync(id);

        var reset = await api.ResetAsync();

        Assert.Equal(HttpStatusCode.NotFound, reset.Status);
        Assert.Equal("frozen", (await api.GetCardAsync(id)).Body!.Status);
        Assert.Equal(1, await _factory.Database.CountAuditEventsAsync());
    }

    // T-I-11 · PR-05 · integration
    // Given CardLab:TestEndpoints=false and payment, quote, event and bank rows inserted directly (raw SQL through Database), When POST /api/test/reset, Then 404 and every payment and bank row count is unchanged
    // Catches: a product path deleting compliance records (AC-37, R-14, CD-5)
    [Fact(Skip = "todo")]
    public void Reset_is_unavailable_and_records_survive_when_disabled()
    {
    }
}
