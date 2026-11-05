using System.Text.RegularExpressions;
using CardLab.Domain.Application;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Application;

public sealed class AuthorisationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Today = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly StoreSeedCard _seedCard;
    private readonly InMemoryCardStore _store;
    private readonly FakeClock _clock = new(Now);
    private readonly FakeCardNumberLookup _lookup = new();
    private readonly CapturingLogger<AuthorisationService> _logger = new();
    private readonly AuthorisationService _service;

    public AuthorisationServiceTests()
        : this(frozen: false)
    {
    }

    private AuthorisationServiceTests(bool frozen)
    {
        var builder = CardBuilder.From(Seed.OpsVan);
        _seedCard = frozen ? builder.Frozen().Build() : builder.Build();
        _store = new InMemoryCardStore(_seedCard);
        _service = new AuthorisationService(_store, _clock, _lookup, _logger);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CardId CardId => _seedCard.Id;

    private static AuthorisationRequestBuilder Request(string amount = "10.00") =>
        new AuthorisationRequestBuilder().WithAmount(amount).WithTimestamp("2026-11-04T10:00:00Z");

    private async Task GivenAsync(SpendLimit[] limits, params NewTransaction[] history)
    {
        await _store.WithCardLockAsync(
            CardId,
            w =>
            {
                foreach (var limit in limits)
                {
                    w.UpsertLimit(limit);
                }

                foreach (var transaction in history)
                {
                    w.AppendTransaction(transaction);
                }

                return Task.FromResult(true);
            },
            Ct);
    }

    private static SpendLimit FuelDay50 => new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("50.00").Build();

    private static NewTransaction Approved(string amount, DateTimeOffset at) => new TransactionBuilder().Amount(amount).In(Category.Fuel).At(at).Build();

    // T-U-19 · PR-08 · unit (application)
    // Given Fuel/Day £50.00 with £50.00 approved at 2026-11-04T10:00:00Z and FakeClock at 2026-11-04T12:00Z, When AuthoriseAsync £10.00 Fuel with timestamp 2026-11-05T10:00:00Z, Then approved
    // Catches: period derived from the server clock instead of the request timestamp (AC-33, R-13)
    [Fact]
    public async Task Period_comes_from_the_request_timestamp()
    {
        await GivenAsync([FuelDay50], Approved("50.00", Today));

        var result = await _service.AuthoriseAsync(Request("10.00").WithTimestamp("2026-11-05T10:00:00Z").Build(), Ct);

        Assert.Equal(Outcome.Approved, result.Decision.Outcome);
        Assert.Equal(Reason.Approved, result.Decision.Reason);
    }

    // T-U-40 · PR-08 · unit (application)
    // Given a request with amount "0.00", When AuthoriseAsync, Then declined invalid_request, problems [amount], TransactionId null, store TransactionCount 0, FakeCardNumberLookup.Calls 0
    // Catches: invalid requests stored or reaching the lookup (AC-20)
    [Fact]
    public async Task Invalid_request_stores_nothing()
    {
        var result = await _service.AuthoriseAsync(Request("0.00").Build(), Ct);

        Assert.Equal(Outcome.Declined, result.Decision.Outcome);
        Assert.Equal(Reason.InvalidRequest, result.Decision.Reason);
        Assert.Empty(result.Decision.Limits);
        Assert.Equal(["amount"], result.Problems.Select(p => p.Field));
        Assert.Null(result.TransactionId);
        Assert.Equal(0, _store.TransactionCount);
        Assert.Equal(0, _lookup.Calls);
    }

    // T-U-41 · PR-08 · unit (application)
    // Given Seed.UnknownCardNumber, When AuthoriseAsync, Then declined card_not_found, empty limits, nothing stored
    // Catches: unknown cards creating transactions (AC-19)
    [Fact]
    public async Task Unknown_card_number_stores_nothing()
    {
        var result = await _service.AuthoriseAsync(Request().WithCardNumber(Seed.UnknownCardNumber).Build(), Ct);

        Assert.Equal(Outcome.Declined, result.Decision.Outcome);
        Assert.Equal(Reason.CardNotFound, result.Decision.Reason);
        Assert.Empty(result.Decision.Limits);
        Assert.Empty(result.Problems);
        Assert.Null(result.TransactionId);
        Assert.Equal(0, _store.TransactionCount);
    }

    // T-U-42 · PR-08 · unit (application)
    // Given Fuel/Day £50.00 with £30.00 approved, When AuthoriseAsync £20.00 Fuel, Then approved with Fuel/Day remaining £0.00, a TransactionId that appears first in ListTransactionsAsync with outcome Approved, DecidedAtUtc = clock
    // Catches: decision not stored or wrong id returned (AC-10, R-7)
    [Fact]
    public async Task Approved_decision_is_stored_and_returned()
    {
        await GivenAsync([FuelDay50], Approved("30.00", Today));

        var result = await _service.AuthoriseAsync(Request("20.00").Build(), Ct);

        Assert.Equal(Outcome.Approved, result.Decision.Outcome);
        var limit = Assert.Single(result.Decision.Limits);
        Assert.Equal(new LimitRemaining(Category.Fuel, Period.Day, TestMoney.Of("50.00"), TestMoney.Of("0.00")), limit);
        Assert.NotNull(result.TransactionId);
        var newest = (await _store.ListTransactionsAsync(CardId, Ct))[0];
        Assert.Equal(result.TransactionId, newest.Id);
        Assert.Equal(Outcome.Approved, newest.Outcome);
        Assert.Equal(TestMoney.Of("20.00"), newest.Amount);
        Assert.Equal(Now, newest.DecidedAtUtc);
        Assert.Equal(Today, newest.OccurredAt);
    }

    // T-U-43 · PR-08 · unit (application)
    // Given the same card, When £20.01 then £20.00 Fuel, Then the first is declined limit_exceeded and stored as Declined, the second approved
    // Catches: declined rows counted by the service's spend query (AC-11, AC-12)
    [Fact]
    public async Task Declined_decision_is_stored_and_does_not_count()
    {
        await GivenAsync([FuelDay50], Approved("30.00", Today));

        var first = await _service.AuthoriseAsync(Request("20.01").Build(), Ct);
        var second = await _service.AuthoriseAsync(Request("20.00").Build(), Ct);

        Assert.Equal(Reason.LimitExceeded, first.Decision.Reason);
        Assert.Equal(TestMoney.Of("20.00"), Assert.Single(first.Decision.Limits).Remaining);
        var history = await _store.ListTransactionsAsync(CardId, Ct);
        var stored = Assert.Single(history, t => t.Id == first.TransactionId);
        Assert.Equal(Outcome.Declined, stored.Outcome);
        Assert.Equal(Reason.LimitExceeded, stored.Reason);
        Assert.Equal(Outcome.Approved, second.Decision.Outcome);
    }

    // T-U-44 · PR-08 · unit (application)
    // Given a frozen card and Fuel/Day + Fuel/Month + Travel/Day limits, When AuthoriseAsync £1.00 Fuel, Then declined card_frozen, limits list has only the two Fuel limits, stored as Declined
    // Catches: other categories reported or frozen not stored (AC-4, R-7)
    [Fact]
    public async Task Frozen_card_declines_and_reports_only_category_limits()
    {
        var frozen = new AuthorisationServiceTests(frozen: true);
        await frozen.GivenAsync([
            FuelDay50,
            new LimitBuilder().For(Category.Fuel).Per(Period.Month).Of("200.00").Build(),
            new LimitBuilder().For(Category.Travel).Per(Period.Day).Of("80.00").Build()]);

        var result = await frozen._service.AuthoriseAsync(Request("1.00").Build(), Ct);

        Assert.Equal(Outcome.Declined, result.Decision.Outcome);
        Assert.Equal(Reason.CardFrozen, result.Decision.Reason);
        Assert.Equal(
            [(Category.Fuel, Period.Day), (Category.Fuel, Period.Month)],
            result.Decision.Limits.Select(l => (l.Category, l.Period)).OrderBy(x => x.Item2));
        var stored = Assert.Single(await frozen._store.ListTransactionsAsync(frozen.CardId, Ct));
        Assert.Equal(result.TransactionId, stored.Id);
        Assert.Equal(Outcome.Declined, stored.Outcome);
        Assert.Equal(Reason.CardFrozen, stored.Reason);
    }

    // T-U-45 · PR-08 · unit (application)
    // Given any of the decisions above, When the CapturingLogger is inspected, Then exactly one AuthorisationDecided per call with CardId, LastFour, Category, Amount, Outcome, Reason, DurationMs; CardId/LastFour "-" for unknown/invalid; no field or message contains a 16-digit run
    // Catches: missing/duplicated events or PAN in logs (AC-39, NFR-1, NFR-7)
    [Fact]
    public async Task Logs_exactly_one_event_without_the_card_number()
    {
        await GivenAsync([FuelDay50], Approved("30.00", Today));

        await _service.AuthoriseAsync(Request("20.00").Build(), Ct);
        await _service.AuthoriseAsync(Request("20.01").Build(), Ct);
        await _service.AuthoriseAsync(Request().WithCardNumber(Seed.UnknownCardNumber).Build(), Ct);
        await _service.AuthoriseAsync(Request("0.00").Build(), Ct);

        var events = _logger.Events("AuthorisationDecided");
        Assert.Equal(4, events.Count);
        Assert.Equal(4, _logger.Entries.Count);

        string[] cardId = [CardId.Value.ToString(), CardId.Value.ToString(), "-", "-"];
        string[] lastFour = ["0001", "0001", "-", "-"];
        string[] category = ["fuel", "fuel", "fuel", "-"];
        string[] amount = ["20.00", "20.01", "10.00", "-"];
        string[] outcome = ["approved", "declined", "declined", "declined"];
        string[] reason = ["approved", "limit_exceeded", "card_not_found", "invalid_request"];
        for (var i = 0; i < events.Count; i++)
        {
            var fields = events[i].Fields;
            Assert.Equal(cardId[i], fields["CardId"]);
            Assert.Equal(lastFour[i], fields["LastFour"]);
            Assert.Equal(category[i], fields["Category"]);
            Assert.Equal(amount[i], fields["Amount"]);
            Assert.Equal(outcome[i], fields["Outcome"]);
            Assert.Equal(reason[i], fields["Reason"]);
            Assert.IsType<long>(fields["DurationMs"]);
            Assert.DoesNotMatch(new Regex("[0-9]{16}"), events[i].Message);
            Assert.All(fields.Values, v => Assert.DoesNotMatch(new Regex("[0-9]{16}"), Convert.ToString(v) ?? string.Empty));
        }
    }

    // T-U-46 · PR-08 · unit (application)
    // Given a card id and a SimulatedPurchase without timestamp, When AuthoriseByCardIdAsync, Then the stored OccurredAt equals FakeClock.UtcNow (whole seconds) and the decision equals the processor path for the same state; unknown id → card_not_found, nothing stored
    // Catches: simulate path diverging from the processor path (DEC-14, AC-22)
    [Fact]
    public async Task Simulate_by_card_id_uses_the_same_path()
    {
        _clock.Set(new DateTimeOffset(2026, 11, 4, 12, 0, 0, 750, TimeSpan.Zero));
        await GivenAsync([FuelDay50], Approved("30.00", Today));
        var purchase = new SimulatedPurchase("20.00", "fuel", "Test Fuel Ltd", null);

        var simulated = await _service.AuthoriseByCardIdAsync(CardId, purchase, Ct);
        var processor = await new AuthorisationServiceTests().ProcessorDecisionForSameStateAsync();

        Assert.Equal(processor.Decision.Outcome, simulated.Decision.Outcome);
        Assert.Equal(processor.Decision.Reason, simulated.Decision.Reason);
        Assert.Equal(processor.Decision.Limits, simulated.Decision.Limits);
        var stored = (await _store.ListTransactionsAsync(CardId, Ct))[0];
        Assert.Equal(simulated.TransactionId, stored.Id);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), stored.OccurredAt);

        var before = _store.TransactionCount;
        var unknown = await _service.AuthoriseByCardIdAsync(new CardId(Guid.Parse("00000000-0000-0000-0000-00000000dead")), purchase, Ct);
        Assert.Equal(Reason.CardNotFound, unknown.Decision.Reason);
        Assert.Equal(Outcome.Declined, unknown.Decision.Outcome);
        Assert.Null(unknown.TransactionId);
        Assert.Equal(before, _store.TransactionCount);
    }

    private async Task<AuthorisationResult> ProcessorDecisionForSameStateAsync()
    {
        await GivenAsync([FuelDay50], Approved("30.00", Today));
        return await _service.AuthoriseAsync(Request("20.00").WithTimestamp("2026-11-04T12:00:00Z").Build(), Ct);
    }
}
