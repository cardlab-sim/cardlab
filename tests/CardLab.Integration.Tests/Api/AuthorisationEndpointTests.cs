using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class AuthorisationEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthorisationEndpointTests(ApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyList<TransactionRecord>> HistoryAsync(CardId id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICardStore>().ListTransactionsAsync(id, Ct);
    }

    private async Task<Money> ApprovedFuelSpendAsync(CardId id, DateTimeOffset day)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICardStore>();
        return await store.WithCardLockAsync(id, w => w.SumApprovedSpendAsync(Category.Fuel, PeriodCalendar.RangeContaining(day, Period.Day), Ct), Ct);
    }

    private static AuthorisationRequest FuelRequest(SeedCard card, string amount, string timestamp) =>
        new AuthorisationRequestBuilder().WithCardNumber(card.CardNumber).WithAmount(amount).WithCategory("fuel").WithTimestamp(timestamp).Build();

    // T-I-2 · PR-08 · integration
    // Given Field sales frozen (Database.SetStatusAsync) with no limits, When POST /api/authorisations £1.00 fuel, Then 200 {outcome declined, reason card_frozen, transactionId set}, the transaction is first in the card's stored history (ICardStore.ListTransactionsAsync from a DI scope) as declined, and approved Fuel spend is unchanged
    // Catches: frozen decision not stored or counted as spend (AC-4)
    [Fact]
    public async Task Frozen_card_is_declined_and_stored()
    {
        await _factory.Database.SetStatusAsync(TestSeed.FieldSales.Name, CardStatus.Frozen);
        var id = await _factory.Database.CardIdAsync(TestSeed.FieldSales.Name);
        var at = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        var spendBefore = await ApprovedFuelSpendAsync(id, at);

        var response = await _factory.CreateApiClient().AuthoriseAsync(FuelRequest(TestSeed.FieldSales, "1.00", "2026-11-04T10:00:00Z"));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.Body);
        Assert.Equal("declined", response.Body.Outcome);
        Assert.Equal("card_frozen", response.Body.Reason);
        Assert.NotNull(response.Body.TransactionId);
        var newest = (await HistoryAsync(id))[0];
        Assert.Equal(response.Body.TransactionId, newest.Id.Value);
        Assert.Equal(Outcome.Declined, newest.Outcome);
        Assert.Equal(spendBefore, await ApprovedFuelSpendAsync(id, at));
    }

    // T-I-2 · PR-08 · integration
    // Given Field sales frozen and then set Active again, When POST /api/authorisations within limits, Then 200 approved
    // Catches: status read from a stale cache (AC-5)
    [Fact]
    public async Task Unfrozen_card_is_approved()
    {
        var client = _factory.CreateApiClient();
        await _factory.Database.SetStatusAsync(TestSeed.FieldSales.Name, CardStatus.Frozen);
        var whileFrozen = await client.AuthoriseAsync(FuelRequest(TestSeed.FieldSales, "1.00", "2026-11-04T10:00:00Z"));
        await _factory.Database.SetStatusAsync(TestSeed.FieldSales.Name, CardStatus.Active);

        var response = await client.AuthoriseAsync(FuelRequest(TestSeed.FieldSales, "1.00", "2026-11-04T10:00:00Z"));

        Assert.Equal("card_frozen", whileFrozen.Body?.Reason);
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("approved", response.Body?.Outcome);
        Assert.Equal("approved", response.Body?.Reason);
    }

    // T-I-4 · PR-08 · integration
    // Given Ops van Fuel/Day £50.00 and £30.00 approved today (London), When £20.00, £20.01 then £20.00 fuel arrive that day, Then approved remaining "0.00"; then (fresh class state: £30.00) declined limit_exceeded remaining "20.00" stored as declined; then approved
    // Catches: SQL spend sum including declines or wrong period range (AC-10, AC-11, AC-12)
    [Fact]
    public async Task Limit_arithmetic_end_to_end()
    {
        var db = _factory.Database;
        var client = _factory.CreateApiClient();
        await db.SeedLimitAsync(TestSeed.OpsVan.Name, Category.Fuel, Period.Day, 50.00m);
        var id = await db.CardIdAsync(TestSeed.OpsVan.Name);
        var dayA = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        var dayB = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        await db.SeedTransactionsAsync(
            TestSeed.OpsVan.Name,
            [
                new TransactionBuilder().Amount("30.00").In(Category.Fuel).At(dayA).DecidedAt(dayA).Build(),
                new TransactionBuilder().Amount("30.00").In(Category.Fuel).At(dayB).DecidedAt(dayB).Build(),
            ]);

        var exactFit = await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan, "20.00", "2026-11-04T10:00:00Z"));
        var overBy1p = await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan, "20.01", "2026-11-05T10:00:00Z"));
        var afterDecline = await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan, "20.00", "2026-11-05T10:00:00Z"));

        Assert.Equal("approved", exactFit.Body?.Outcome);
        Assert.Equal(new LimitRemainingDto("fuel", "day", "50.00", "0.00"), Assert.Single(exactFit.Body!.Limits));
        Assert.Equal("declined", overBy1p.Body?.Outcome);
        Assert.Equal("limit_exceeded", overBy1p.Body?.Reason);
        Assert.Equal(new LimitRemainingDto("fuel", "day", "50.00", "20.00"), Assert.Single(overBy1p.Body!.Limits));
        var declined = Assert.Single(await HistoryAsync(id), t => t.Id.Value == overBy1p.Body.TransactionId);
        Assert.Equal(Outcome.Declined, declined.Outcome);
        Assert.Equal("approved", afterDecline.Body?.Outcome);
    }

    // T-I-5 · PR-08 · integration
    // Given Seed.UnknownCardNumber, When POST /api/authorisations, Then 404 {declined, card_not_found, transactionId null, limits []} and Database.CountTransactionsAsync unchanged
    // Catches: unknown card stored or 200 (AC-19, DEC-14)
    [Fact]
    public async Task Unknown_card_is_404_and_stores_nothing()
    {
        var before = await _factory.Database.CountTransactionsAsync();

        var response = await _factory.CreateApiClient().AuthoriseAsync(
            new AuthorisationRequestBuilder().WithCardNumber(TestSeed.UnknownCardNumber).Build());

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.NotNull(response.Body);
        Assert.Equal("declined", response.Body.Outcome);
        Assert.Equal("card_not_found", response.Body.Reason);
        Assert.Null(response.Body.TransactionId);
        Assert.Empty(response.Body.Limits);
        Assert.Empty(response.Body.Problems);
        Assert.DoesNotContain(TestSeed.UnknownCardNumber, response.Raw);
        Assert.Equal(before, await _factory.Database.CountTransactionsAsync());
    }

    // T-I-5 · PR-08 · integration (theory)
    // Given requests with amount missing, "0.00", category "petrol", timestamp "not-a-time", When POST /api/authorisations, Then 400 {declined, invalid_request, problems:[{field,…}]} and no transaction stored
    // Catches: malformed requests stored, or framework 400s without the decision body (AC-20)
    [Theory]
    [InlineData(null, "fuel", "2026-11-04T10:00:00Z", "amount")]
    [InlineData("0.00", "fuel", "2026-11-04T10:00:00Z", "amount")]
    [InlineData("5.00", "petrol", "2026-11-04T10:00:00Z", "category")]
    [InlineData("5.00", "fuel", "not-a-time", "timestamp")]
    public async Task Malformed_requests_are_400_and_store_nothing(string? amount, string category, string timestamp, string field)
    {
        var before = await _factory.Database.CountTransactionsAsync();
        var request = new AuthorisationRequestBuilder().WithAmount(amount).WithCategory(category).WithTimestamp(timestamp).Build();

        var response = await _factory.CreateApiClient().AuthoriseAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.NotNull(response.Body);
        Assert.Equal("declined", response.Body.Outcome);
        Assert.Equal("invalid_request", response.Body.Reason);
        Assert.Null(response.Body.TransactionId);
        Assert.Equal([field], response.Body.Problems.Select(p => p.Field));
        Assert.Equal(before, await _factory.Database.CountTransactionsAsync());
    }

    // BUG-005 · PR-08 · integration
    // Given a valid request whose merchant is a card number (plain and spaced), When POST /api/authorisations, Then 400 invalid_request with problems [merchant], nothing stored, the digits not echoed
    // Catches: a PAN-shaped merchant stored and later displayed (NFR-1, AC-24)
    [Theory]
    [InlineData("9999000100000001")]
    [InlineData("9999 0001 0000 0001")]
    public async Task Card_number_like_merchant_is_rejected_and_not_stored(string merchant)
    {
        var before = await _factory.Database.CountTransactionsAsync();
        var request = FuelRequest(TestSeed.OpsVan, "5.00", "2026-11-04T10:00:00Z") with { Merchant = merchant };

        var response = await _factory.CreateApiClient().AuthoriseAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("invalid_request", response.Body?.Reason);
        Assert.Equal(["merchant"], response.Body!.Problems.Select(p => p.Field));
        Assert.DoesNotContain("0001", response.Raw);
        Assert.Equal(before, await _factory.Database.CountTransactionsAsync());
    }

    // T-I-9 · PR-08 · integration
    // Given Marketing Fuel/Day £50.00, When two identical £30.00 fuel requests (same merchant and timestamp) arrive one after the other, Then approved then declined limit_exceeded, both in the history
    // Catches: accidental idempotency/deduplication (AC-32)
    [Fact]
    public async Task Repeated_identical_request_is_a_second_transaction()
    {
        await _factory.Database.SeedLimitAsync(TestSeed.Marketing.Name, Category.Fuel, Period.Day, 50.00m);
        var id = await _factory.Database.CardIdAsync(TestSeed.Marketing.Name);
        var client = _factory.CreateApiClient();
        var request = FuelRequest(TestSeed.Marketing, "30.00", "2026-11-04T10:00:00Z");

        var first = await client.AuthoriseAsync(request);
        var second = await client.AuthoriseAsync(request);

        Assert.Equal("approved", first.Body?.Outcome);
        Assert.Equal("declined", second.Body?.Outcome);
        Assert.Equal("limit_exceeded", second.Body?.Reason);
        Assert.NotEqual(first.Body!.TransactionId, second.Body!.TransactionId);
        var ids = (await HistoryAsync(id)).Select(t => t.Id.Value).ToList();
        Assert.Contains(first.Body.TransactionId!.Value, ids);
        Assert.Contains(second.Body.TransactionId!.Value, ids);
    }
}
