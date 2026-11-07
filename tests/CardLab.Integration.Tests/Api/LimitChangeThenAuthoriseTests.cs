using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class LimitChangeThenAuthoriseTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LimitChangeThenAuthoriseTests(ApiFactory factory) => _factory = factory;

    private static AuthorisationRequest FuelRequest(SeedCard card, string amount) =>
        new AuthorisationRequestBuilder().WithCardNumber(card.CardNumber).WithAmount(amount).WithCategory("fuel").WithTimestamp("2026-11-04T10:00:00Z").Build();

    /// <summary>Fuel/day £50.00 set through the API, then £25.00 + £15.00 approved through the processor path.</summary>
    private async Task<Guid> CardWithFortyPoundsSpentAsync(SeedCard card)
    {
        var client = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync(card.Name)).Value;
        Assert.Equal(HttpStatusCode.OK, (await client.SetLimitAsync(id, "fuel", "day", "50.00")).Status);
        Assert.Equal("approved", (await client.AuthoriseAsync(FuelRequest(card, "25.00"))).Body?.Outcome);
        Assert.Equal("approved", (await client.AuthoriseAsync(FuelRequest(card, "15.00"))).Body?.Outcome);
        return id;
    }

    // T-I-8 · PR-11 · integration
    // Given Ops van with PUT fuel/day "50.00" and £40.00 approved fuel today (via POST /api/authorisations), When PUT fuel/day "30.00" then a £0.01 fuel authorisation today, Then the limit is "30.00", the two earlier transactions are unchanged in GET transactions, and the response is declined limit_exceeded with remaining "0.00"
    // Catches: lowering below spend rejected or rewriting history (AC-30, R-13)
    [Fact]
    public async Task Lowering_a_limit_below_spend_declines_further_spend()
    {
        var client = _factory.CreateApiClient();
        var id = await CardWithFortyPoundsSpentAsync(TestSeed.OpsVan);
        var before = (await client.ListTransactionsAsync(id)).Body!;

        var lowered = await client.SetLimitAsync(id, "fuel", "day", "30.00");
        var decision = await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan, "0.01"));

        Assert.Equal(HttpStatusCode.OK, lowered.Status);
        Assert.Equal("30.00", lowered.Body?.Amount);
        Assert.Equal(HttpStatusCode.OK, decision.Status);
        Assert.Equal("declined", decision.Body?.Outcome);
        Assert.Equal("limit_exceeded", decision.Body?.Reason);
        Assert.Equal(new LimitRemainingDto("fuel", "day", "30.00", "0.00"), Assert.Single(decision.Body!.Limits));
        Assert.Equal(["15.00", "25.00"], before.Select(t => t.Amount).Order());
        Assert.All(before, t => Assert.Equal("approved", t.Outcome));
        var after = (await client.ListTransactionsAsync(id)).Body!;
        Assert.Equal(3, after.Count);
        var declinedId = decision.Body.TransactionId;
        Assert.Equal(before.OrderBy(t => t.Id), after.Where(t => t.Id != declinedId).OrderBy(t => t.Id));
        Assert.Equal("declined", after.Single(t => t.Id == declinedId).Outcome);
        Assert.Equal("30.00", (await client.GetCardAsync(id)).Body!.Limits.Single().Amount);
    }

    // T-I-8 · PR-11 · integration
    // Given a card in the AC-30 state (built here on Marketing: limit 30.00, £40.00 spent), When DELETE fuel/day then a £1,000.00 fuel authorisation today, Then approved with limits []
    // Catches: removed limit still enforced (cache) (AC-31)
    [Fact]
    public async Task Removing_a_limit_makes_the_category_unlimited_at_once()
    {
        var client = _factory.CreateApiClient();
        var id = await CardWithFortyPoundsSpentAsync(TestSeed.Marketing);
        await client.SetLimitAsync(id, "fuel", "day", "30.00");
        Assert.Equal("limit_exceeded", (await client.AuthoriseAsync(FuelRequest(TestSeed.Marketing, "0.01"))).Body?.Reason);

        var removed = await client.RemoveLimitAsync(id, "fuel", "day");
        var decision = await client.AuthoriseAsync(FuelRequest(TestSeed.Marketing, "1000.00"));

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal(HttpStatusCode.OK, decision.Status);
        Assert.Equal("approved", decision.Body?.Outcome);
        Assert.Empty(decision.Body!.Limits);
    }
}
