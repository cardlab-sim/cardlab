using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

// Use [Property(Arbitrary = [typeof(CardLabArbitraries), typeof(PaymentArbitraries)])] once PaymentArbitraries exists (PR-01).
public sealed class PaymentProperties
{
    // T-P-1 · PR-01 · property
    // Given any sequence of approved purchase amounts and completed payment amounts, When BalanceRules.Owed is applied to the running sums, Then it equals max(£0.00, Σpurchases − Σcompleted) and is never negative
    // Catches: balance arithmetic slips for any sequence (AC-25, NFR-1)
    [Property(Skip = "todo")]
    public bool Balance_is_purchases_minus_completed_payments_floored() => true;

    // T-P-2 · PR-01 · property
    // Given any ConfirmScript (quotes, confirms with repeated references, purchases in between, settlements) run against a pure model driven by ConfirmRules.Decide and PaymentStateMachine.Apply, When it completes, Then each reference has at most one payment
    // Catches: a repeated confirm creating a second payment (AC-25, R-7)
    [Property(Skip = "todo")]
    public bool At_most_one_payment_per_reference() => true;

    // T-P-3 · PR-01 · property
    // Given the same model and any ConfirmScript, When each step is applied, Then the card never has more than one pending payment
    // Catches: two payments in flight on one card (AC-25, R-8)
    [Property(Skip = "todo")]
    public bool At_most_one_pending_payment_per_card() => true;

    // T-P-4 · PR-01 · property
    // Given any state reached from Pending and any sequence of bank outcomes, When applied in order, Then once Completed or Failed every further Apply fails and the state never changes
    // Catches: terminal states changing (AC-25, AC-17, R-6)
    [Property(Skip = "todo")]
    public bool Completed_and_failed_never_change() => true;
}
