namespace CardLab.Domain.Tests.Application;

public sealed class AuthorisationServiceTests
{
    // T-U-19 · PR-08 · unit (application)
    // Given Fuel/Day £50.00 with £50.00 approved at 2026-11-04T10:00:00Z and FakeClock at 2026-11-04T12:00Z, When AuthoriseAsync £10.00 Fuel with timestamp 2026-11-05T10:00:00Z, Then approved
    // Catches: period derived from the server clock instead of the request timestamp (AC-33, R-13)
    [Fact(Skip = "todo")]
    public void Period_comes_from_the_request_timestamp()
    {
    }

    // T-U-40 · PR-08 · unit (application)
    // Given a request with amount "0.00", When AuthoriseAsync, Then declined invalid_request, problems [amount], TransactionId null, store TransactionCount 0, FakeCardNumberLookup.Calls 0
    // Catches: invalid requests stored or reaching the lookup (AC-20)
    [Fact(Skip = "todo")]
    public void Invalid_request_stores_nothing()
    {
    }

    // T-U-41 · PR-08 · unit (application)
    // Given Seed.UnknownCardNumber, When AuthoriseAsync, Then declined card_not_found, empty limits, nothing stored
    // Catches: unknown cards creating transactions (AC-19)
    [Fact(Skip = "todo")]
    public void Unknown_card_number_stores_nothing()
    {
    }

    // T-U-42 · PR-08 · unit (application)
    // Given Fuel/Day £50.00 with £30.00 approved, When AuthoriseAsync £20.00 Fuel, Then approved with Fuel/Day remaining £0.00, a TransactionId that appears first in ListTransactionsAsync with outcome Approved, DecidedAtUtc = clock
    // Catches: decision not stored or wrong id returned (AC-10, R-7)
    [Fact(Skip = "todo")]
    public void Approved_decision_is_stored_and_returned()
    {
    }

    // T-U-43 · PR-08 · unit (application)
    // Given the same card, When £20.01 then £20.00 Fuel, Then the first is declined limit_exceeded and stored as Declined, the second approved
    // Catches: declined rows counted by the service's spend query (AC-11, AC-12)
    [Fact(Skip = "todo")]
    public void Declined_decision_is_stored_and_does_not_count()
    {
    }

    // T-U-44 · PR-08 · unit (application)
    // Given a frozen card and Fuel/Day + Fuel/Month + Travel/Day limits, When AuthoriseAsync £1.00 Fuel, Then declined card_frozen, limits list has only the two Fuel limits, stored as Declined
    // Catches: other categories reported or frozen not stored (AC-4, R-7)
    [Fact(Skip = "todo")]
    public void Frozen_card_declines_and_reports_only_category_limits()
    {
    }

    // T-U-45 · PR-08 · unit (application)
    // Given any of the decisions above, When the CapturingLogger is inspected, Then exactly one AuthorisationDecided per call with CardId, LastFour, Category, Amount, Outcome, Reason, DurationMs; CardId/LastFour "-" for unknown/invalid; no field or message contains a 16-digit run
    // Catches: missing/duplicated events or PAN in logs (AC-39, NFR-1, NFR-7)
    [Fact(Skip = "todo")]
    public void Logs_exactly_one_event_without_the_card_number()
    {
    }

    // T-U-46 · PR-08 · unit (application)
    // Given a card id and a SimulatedPurchase without timestamp, When AuthoriseByCardIdAsync, Then the stored OccurredAt equals FakeClock.UtcNow (whole seconds) and the decision equals the processor path for the same state; unknown id → card_not_found, nothing stored
    // Catches: simulate path diverging from the processor path (DEC-14, AC-22)
    [Fact(Skip = "todo")]
    public void Simulate_by_card_id_uses_the_same_path()
    {
    }
}
