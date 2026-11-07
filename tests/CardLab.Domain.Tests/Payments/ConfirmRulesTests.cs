namespace CardLab.Domain.Tests.Payments;

public sealed class ConfirmRulesTests
{
    // T-U-6 · PR-01 · unit
    // Given an open £150.50 quote, no payment for it, owed £150.50 and another pending payment PAY-00000009 of £150.50, When Decide, Then RefusePendingExists(PAY-00000009, £150.50)
    // Catches: a second payment in flight on one card (AC-11, R-8)
    [Fact(Skip = "todo")]
    public void Refuses_when_another_payment_is_pending()
    {
    }

    // T-U-33 · PR-01 · unit
    // Given an open £150.50 quote: owed £150.50 → Create; own payment in any state (even with owed £170.50 or status Confirmed) → Repeat; owed £170.50 → RefuseBalanceChanged(£170.50); status Refused (even with owed back at £150.50) → RefuseAlreadyRefused(owed); with several conditions true the precedence is Repeat > AlreadyRefused > PendingExists > BalanceChanged
    // Catches: a repeat creating a second payment, or a refused reference paying later (AC-8, AC-9, AC-30, R-5, R-7)
    [Fact(Skip = "todo")]
    public void Decides_create_repeat_and_refusals_in_precedence_order()
    {
    }
}
