using System.Net;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Infrastructure.FakeBank;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using Microsoft.EntityFrameworkCore;
using TestSeed = CardLab.TestKit.Seed;

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
    [Fact]
    public async Task Reset_clears_payments_and_reseeds_the_bank()
    {
        var api = _factory.CreateApiClient();
        await PaymentRows.InsertAsync(_factory.Database);
        await _factory.Database.SeedTransactionsAsync("Ops van", [new TransactionBuilder().Amount("50.00").Build()]);

        var reset = await api.ResetAsync();

        Assert.Equal(HttpStatusCode.NoContent, reset.Status);
        foreach (var table in new[] { "PaymentQuote", "Payment", "PaymentEvent", "BankDebitRequest", "BankEntry" })
        {
            Assert.Equal(0, await _factory.Database.CountRowsAsync(table));
        }

        await using var db = _factory.Database.CreateDbContext();
        var bank = await db.BankAccounts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var steering = await db.BankSteerings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((TestSeed.BankAccountRef, 25000.00m), (bank.Id, bank.AvailableBalance));
        Assert.Equal((null, null), (steering.NextDebitOutcome, steering.NextStatusCheck));
        Assert.Equal(1, await _factory.Database.CountRowsAsync("LinkedAccount"));
        var store = new SqlCardStore(db);
        foreach (var card in TestSeed.Cards)
        {
            var id = await _factory.Database.CardIdAsync(card.Name);
            Assert.Equal("0.00", (await store.GetBalanceAsync(id, TestContext.Current.CancellationToken))!.Owed.ToString());
        }
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
    [Fact]
    public async Task Reset_is_unavailable_and_records_survive_when_disabled()
    {
        var api = _factory.CreateApiClient();
        await PaymentRows.InsertAsync(_factory.Database);

        var reset = await api.ResetAsync();

        Assert.Equal(HttpStatusCode.NotFound, reset.Status);
        var counts = new[]
        {
            await _factory.Database.CountRowsAsync("PaymentQuote"),
            await _factory.Database.CountRowsAsync("Payment"),
            await _factory.Database.CountRowsAsync("PaymentEvent"),
            await _factory.Database.CountRowsAsync("BankDebitRequest"),
            await _factory.Database.CountRowsAsync("BankEntry"),
        };
        Assert.Equal([2, 2, 1, 1, 1], counts);
        await using var db = _factory.Database.CreateDbContext();
        var bank = await db.BankAccounts.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(24990.00m, bank.AvailableBalance);
    }
}

// Arrange helper: a completed and a pending payment on Ops van with their quotes, an event, the bank's request and
// ledger entry, a reduced bank balance and set steering, written straight to the tables.
internal static class PaymentRows
{
    private static readonly DateTime At = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    public static async Task InsertAsync(SqlTestDatabase database)
    {
        await using var db = database.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Name == TestSeed.OpsVan.Name, ct);
        var linked = await db.LinkedAccounts.AsNoTracking().SingleAsync(ct);
        db.PaymentQuotes.AddRange(Quote("PAY-RESET001", card.Id, linked.Id), Quote("PAY-RESET002", card.Id, linked.Id));
        db.Payments.AddRange(Payment(card.Id, "PAY-RESET001", "Completed"), Payment(card.Id, "PAY-RESET002", "Pending"));
        db.PaymentEvents.Add(new PaymentEventRow { Id = Guid.NewGuid(), Reference = "PAY-RESET001", AtUtc = At, Kind = "ConfirmCreated" });
        db.BankDebitRequests.Add(new BankDebitRequestRow { Reference = "PAY-RESET001", AccountId = TestSeed.BankAccountRef, Amount = 10m, Outcome = "Completed", ReceivedAtUtc = At, DebitedAtUtc = At });
        db.BankEntries.Add(new BankEntryRow { Id = Guid.NewGuid(), AccountId = TestSeed.BankAccountRef, Reference = "PAY-RESET001", Amount = 10m, Narrative = "CardLab card payment PAY-RESET001", PostedAtUtc = At });
        await db.SaveChangesAsync(ct);
        await db.BankAccounts.ExecuteUpdateAsync(s => s.SetProperty(b => b.AvailableBalance, 24990m), ct);
        await db.BankSteerings.ExecuteUpdateAsync(
            s => s.SetProperty(b => b.NextDebitOutcome, nameof(NextDebitOutcome.Complete)).SetProperty(b => b.NextStatusCheck, nameof(NextStatusCheck.DoesNotAnswer)),
            ct);
    }

    private static PaymentQuoteRow Quote(string reference, Guid cardId, Guid linkedAccountId) => new()
    {
        Reference = reference,
        CardId = cardId,
        LinkedAccountId = linkedAccountId,
        Kind = "Full",
        Amount = 10m,
        Fee = 0m,
        Total = 10m,
        FromLabel = "Brightline Supplies Ltd current account, ending 7731",
        ToLabel = "Ops van card, ending 0001",
        WordingVersion = "v1",
        Heading = "Pay your full balance",
        RenderedLines = "Amount: 10.00",
        ButtonLabel = "Confirm payment",
        ActorKind = "Cardholder",
        ActorId = cardId,
        ActorName = "Priya Nair",
        AuthorityBasis = TestSeed.AuthorityBasis,
        Origin = "CardholderView",
        CreatedAtUtc = At,
        Status = "Confirmed",
    };

    private static PaymentRow Payment(Guid cardId, string reference, string state) => new()
    {
        Id = Guid.NewGuid(),
        Reference = reference,
        CardId = cardId,
        Kind = "Full",
        Amount = 10m,
        Fee = 0m,
        State = state,
        CreatedAtUtc = At,
        SettledAtUtc = state == "Completed" ? At : null,
        EffectiveAtUtc = state == "Completed" ? At : null,
        BalanceBefore = state == "Completed" ? 10m : null,
        BalanceAfter = state == "Completed" ? 0m : null,
    };
}
