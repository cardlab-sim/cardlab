using System.Net;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Limits;
using CardLab.Infrastructure.Model;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class CardReadEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CardReadEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-19 · PR-09 · integration
    // Given the seed, When GET /api/cards and GET /api/cards/{id}, Then 3 cards ordered by name with {id, name, cardholderName, lastFour, status:"active"} and the detail with limits []; GET /api/cards/{unknown} → 404 ProblemDetails; no body contains a 16-digit run
    // Catches: wrong wire names/casing (§7), PAN in responses (NFR-1)
    [Fact]
    public async Task Lists_and_reads_cards()
    {
        var api = _factory.CreateApiClient();

        var list = await api.ListCardsAsync();

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.NotNull(list.Body);
        Assert.Equal(["Field sales", "Marketing", "Ops van"], list.Body.Select(c => c.Name).ToArray());
        var opsVan = list.Body.Single(c => c.Name == "Ops van");
        Assert.Equal("Priya Nair", opsVan.CardholderName);
        Assert.Equal("0001", opsVan.LastFour);
        Assert.All(list.Body, c => Assert.Equal("active", c.Status));
        Assert.Contains("\"cardholderName\":\"Priya Nair\"", list.Raw);
        Assert.Contains("\"lastFour\":\"0001\"", list.Raw);

        var detail = await api.GetCardAsync(opsVan.Id);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.NotNull(detail.Body);
        Assert.Equal(opsVan.Id, detail.Body.Id);
        Assert.Equal("Ops van", detail.Body.Name);
        Assert.Equal("active", detail.Body.Status);
        Assert.Empty(detail.Body.Limits);
        Assert.Contains("\"limits\":[]", detail.Raw);

        var unknown = await api.GetCardAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"));

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Contains("\"title\":\"Card not found\"", unknown.Raw);
        Assert.Contains("\"status\":404", unknown.Raw);

        PanScanner.AssertNoPan(list.Raw, "GET /api/cards");
        PanScanner.AssertNoPan(detail.Raw, "GET /api/cards/{id}");
        PanScanner.AssertNoPan(unknown.Raw, "GET /api/cards/{unknown}");
    }

    // T-I-19 · PR-09 · integration
    // Given 2 transactions and 2 audit events seeded with increasing times, When GET transactions / audit, Then newest first with the §7 fields (amount "12.34", category wire name, outcome/reason wire names, occurredAt with its original offset); unknown card id → 404
    // Catches: oldest-first lists (AC-22, AC-23) or enum names leaking ("LimitExceeded") (also PAN, NFR-1)
    [Fact]
    public async Task Lists_transactions_and_audit_newest_first()
    {
        var api = _factory.CreateApiClient();
        var cardId = (await _factory.Database.CardIdAsync("Marketing")).Value;
        var plusOne = TimeSpan.FromHours(1);
        var older = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 11, 4, 14, 30, 0, plusOne);
        await _factory.Database.SeedTransactionsAsync(
            "Marketing",
            [
                new TransactionBuilder().Amount("5.00").In(Category.Travel).At(older).DecidedAt(older).Build(),
                new TransactionBuilder().Amount("12.34").In(Category.MealsAndEntertainment).At(newer).DecidedAt(newer.ToUniversalTime()).Declined(Reason.LimitExceeded).Build(),
            ]);
        await using (var db = _factory.Database.CreateDbContext())
        {
            db.AuditEvents.Add(new AuditEventRow { Id = Guid.NewGuid(), CardId = cardId, ActorId = TestSeed.AdminId, ActorName = TestSeed.AdminName, AtUtc = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc), Action = "Freeze", OldValue = "Active", NewValue = "Frozen" });
            db.AuditEvents.Add(new AuditEventRow { Id = Guid.NewGuid(), CardId = cardId, ActorId = TestSeed.AdminId, ActorName = TestSeed.AdminName, AtUtc = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc), Action = "LimitChanged", OldValue = "Fuel/Day/50.00", NewValue = "Fuel/Day/80.00" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var transactions = await api.ListTransactionsAsync(cardId);
        var audit = await api.ListAuditAsync(cardId);

        Assert.Equal(HttpStatusCode.OK, transactions.Status);
        Assert.NotNull(transactions.Body);
        Assert.Equal(2, transactions.Body.Count);
        var first = transactions.Body[0];
        Assert.Equal("12.34", first.Amount);
        Assert.Equal("meals_and_entertainment", first.Category);
        Assert.Equal("declined", first.Outcome);
        Assert.Equal("limit_exceeded", first.Reason);
        Assert.Equal("Test Fuel Ltd", first.Merchant);
        Assert.Equal(newer.UtcDateTime, first.OccurredAt.UtcDateTime);
        Assert.Equal(plusOne, first.OccurredAt.Offset);
        Assert.Equal("5.00", transactions.Body[1].Amount);
        Assert.Equal("travel", transactions.Body[1].Category);
        Assert.Equal("approved", transactions.Body[1].Outcome);
        Assert.Equal("approved", transactions.Body[1].Reason);

        Assert.Equal(HttpStatusCode.OK, audit.Status);
        Assert.NotNull(audit.Body);
        Assert.Equal(["limit_changed", "freeze"], audit.Body.Select(a => a.Action).ToArray());
        Assert.Equal("Sam Ellis", audit.Body[0].ActorName);
        Assert.Equal("Fuel/Day/50.00", audit.Body[0].OldValue);
        Assert.Equal("Fuel/Day/80.00", audit.Body[0].NewValue);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), audit.Body[0].AtUtc);

        var unknownId = Guid.Parse("00000000-0000-0000-0000-00000000dead");
        Assert.Equal(HttpStatusCode.NotFound, (await api.ListTransactionsAsync(unknownId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await api.ListAuditAsync(unknownId)).Status);
        PanScanner.AssertNoPan(transactions.Raw, "GET transactions");
        PanScanner.AssertNoPan(audit.Raw, "GET audit");
    }
}
