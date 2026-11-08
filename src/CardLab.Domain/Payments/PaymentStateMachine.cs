using CardLab.Domain.Common;

namespace CardLab.Domain.Payments;

/// <summary>A state change produced by a bank outcome.</summary>
/// <param name="From">State before.</param>
/// <param name="To">State after (equal to <paramref name="From"/> when the outcome leaves the payment pending).</param>
/// <param name="Reason">Failure reason when <paramref name="To"/> is Failed; null otherwise.</param>
/// <param name="BankDebitedAtUtc">The bank's debit time for a completed payment when reported; null otherwise.</param>
public sealed record Transition(PaymentState From, PaymentState To, FailureReason? Reason, DateTimeOffset? BankDebitedAtUtc);

/// <summary>The payment lifecycle (R-6). Pure; high-risk path (T-U-7, T-P-4).</summary>
/// <remarks>Implemented in PR-01.</remarks>
public static class PaymentStateMachine
{
    /// <summary>Applies a bank outcome to a payment's current state.</summary>
    /// <remarks>
    /// Pending + Completed → Completed (debit time carried); Pending + Refused(r) → Failed(r); Pending + NotReceived →
    /// Failed(NotReceived); Pending + Unknown → Ok(Pending → Pending, no reason) (no state change; callers log nothing).
    /// Completed or Failed + any outcome → Fail("payment already completed" / "payment already failed"): terminal states
    /// never change (AC-17).
    /// </remarks>
    /// <param name="current">Current state.</param>
    /// <param name="outcome">What the bank said.</param>
    /// <returns>The transition, or an error message for a terminal state.</returns>
    public static Result<Transition, string> Apply(PaymentState current, BankOutcome outcome)
    {
        switch (current)
        {
            case PaymentState.Completed:
                return Result<Transition, string>.Fail("payment already completed");
            case PaymentState.Failed:
                return Result<Transition, string>.Fail("payment already failed");
            case not PaymentState.Pending:
                throw new ArgumentOutOfRangeException(nameof(current), current, "Unknown payment state.");
        }

        var transition = outcome switch
        {
            BankOutcome.Completed completed => new Transition(current, PaymentState.Completed, null, completed.DebitedAtUtc),
            BankOutcome.Refused refused => new Transition(current, PaymentState.Failed, refused.Reason, null),
            BankOutcome.NotReceived => new Transition(current, PaymentState.Failed, FailureReason.NotReceived, null),
            BankOutcome.Unknown => new Transition(current, PaymentState.Pending, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown bank outcome."),
        };
        return Result<Transition, string>.Ok(transition);
    }
}
