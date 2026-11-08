namespace CardLab.Domain.Payments;

/// <summary>A pending payment on the card other than the one being confirmed (R-8).</summary>
/// <param name="Reference">Its reference.</param>
/// <param name="Amount">Its amount.</param>
public sealed record PendingPaymentRef(PaymentReference Reference, Money Amount);

/// <summary>What a confirm must do (design §5 <c>ConfirmRules</c>). Closed hierarchy.</summary>
public abstract record ConfirmDecision
{
    private ConfirmDecision()
    {
    }

    /// <summary>Create the pending payment for this reference and call the bank.</summary>
    public sealed record Create : ConfirmDecision;

    /// <summary>A payment already exists for this reference: record the repeat and return the first outcome (R-7).</summary>
    public sealed record Repeat : ConfirmDecision;

    /// <summary>The balance owed differs from the quoted amount: refuse, mark the quote Refused, nothing moves (R-5, W-6).</summary>
    /// <param name="NewOwed">The current balance owed.</param>
    public sealed record RefuseBalanceChanged(Money NewOwed) : ConfirmDecision;

    /// <summary>The quote was refused before: refuse again with the same message (AC-30, W-6).</summary>
    /// <param name="NewOwed">The current balance owed.</param>
    public sealed record RefuseAlreadyRefused(Money NewOwed) : ConfirmDecision;

    /// <summary>Another payment on the card is pending: refuse (R-8, W-9).</summary>
    /// <param name="Reference">The pending payment's reference.</param>
    /// <param name="Amount">The pending payment's amount.</param>
    public sealed record RefusePendingExists(PaymentReference Reference, Money Amount) : ConfirmDecision;
}

/// <summary>Exactly-once and one-in-flight rules for a confirm (R-5, R-7, R-8). Pure; high-risk path (T-P-2, T-P-3).</summary>
/// <remarks>Implemented in PR-01.</remarks>
public static class ConfirmRules
{
    /// <summary>Decides a confirm of <paramref name="quote"/>, evaluated inside the card lock.</summary>
    /// <remarks>
    /// Precedence (PLANNER-DECISION, first match wins): a payment already exists for the quote → <c>Repeat</c>
    /// (whatever its state); quote status Refused → <c>RefuseAlreadyRefused(currentOwed)</c>; another payment pending →
    /// <c>RefusePendingExists</c>; <paramref name="currentOwed"/> ≠ quote amount → <c>RefuseBalanceChanged(currentOwed)</c>;
    /// otherwise <c>Create</c>.
    /// </remarks>
    /// <param name="quote">The stored quote for the reference (already checked to belong to the card).</param>
    /// <param name="status">The quote's current status.</param>
    /// <param name="paymentForQuote">State of the payment created for this reference, or null when none exists.</param>
    /// <param name="otherPending">A pending payment on the card for a different reference, or null.</param>
    /// <param name="currentOwed">Balance owed now (computed inside the lock).</param>
    /// <returns>The decision.</returns>
    public static ConfirmDecision Decide(Quote quote, QuoteStatus status, PaymentState? paymentForQuote, PendingPaymentRef? otherPending, Money currentOwed)
    {
        if (paymentForQuote is not null)
        {
            return new ConfirmDecision.Repeat();
        }

        if (status == QuoteStatus.Refused)
        {
            return new ConfirmDecision.RefuseAlreadyRefused(currentOwed);
        }

        if (otherPending is not null)
        {
            return new ConfirmDecision.RefusePendingExists(otherPending.Reference, otherPending.Amount);
        }

        return currentOwed != quote.Amount
            ? new ConfirmDecision.RefuseBalanceChanged(currentOwed)
            : new ConfirmDecision.Create();
    }
}
