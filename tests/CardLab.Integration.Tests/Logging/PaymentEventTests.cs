using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Logging;

[Collection("sql")]
public sealed class PaymentEventTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public PaymentEventTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-14 · PR-08 · integration
    // Given J-1 (complete) and J-3 (timeout then check) through the API, When the captured OpenTelemetry records are read, Then exactly one PaymentStateChanged per state change (none→pending, pending→completed / failed) with paymentId, cardId, lastFour, kind, amount, fromState, toState, reason, durationMs; BankCallUnanswered for the timeout; no record contains a 16-digit run or "51907731"
    // Catches: missing or duplicated payment events, or numbers in logs (AC-27, NFR-4, NFR-3)
    [Fact(Skip = "todo")]
    public void One_structured_event_per_state_change()
    {
    }
}
