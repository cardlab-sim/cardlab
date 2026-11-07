namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentStateMachineTests
{
    // T-U-7 · PR-01 · unit
    // Given Pending, When Completed(at) / Refused(InsufficientFunds) / Refused(AccountUnavailable) / NotReceived / Unknown, Then Completed with debit time at / Failed(InsufficientFunds) / Failed(AccountUnavailable) / Failed(NotReceived) / Pending → Pending; Given Completed or Failed, When any outcome, Then Fail
    // Catches: a terminal payment changing state, or Unknown failing the payment (AC-17, R-6, R-11)
    [Fact(Skip = "todo")]
    public void Applies_outcomes_and_keeps_terminal_states()
    {
    }
}
