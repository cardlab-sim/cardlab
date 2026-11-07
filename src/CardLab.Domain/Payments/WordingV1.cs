namespace CardLab.Domain.Payments;

/// <summary>
/// Customer-facing wording v1 (requirements §17, approved by the compliance partner, CD-2/CD-3): the only place the
/// strings W-1..W-10 exist (DEC-10). The API returns them; the SPA shows them verbatim; records store them.
/// </summary>
/// <remarks>
/// Implemented in PR-01. Every amount is rendered with <c>Money.ToDisplay()</c> (<c>£1,234.56</c>). The texts must
/// match §17 character for character (they are pasted into the PR-01 brief). High-risk path (NFR-7).
/// </remarks>
public static class WordingV1
{
    /// <summary>The wording version stored with every record.</summary>
    public const string Version = "v1";

    /// <summary>W-1: the review screen (heading, six lines in order, button).</summary>
    /// <param name="amount">Amount (= total; fee £0.00).</param>
    /// <param name="fromLabel">Rendered source label.</param>
    /// <param name="toLabel">Rendered destination label.</param>
    /// <returns>Version "v1", heading "Pay your full balance", lines Amount/Fee/Total to pay/When/From/To, button
    /// "Confirm payment of {amount}".</returns>
    public static RenderedWording ReviewScreen(Money amount, string fromLabel, string toLabel) => throw new NotImplementedException("PR-01");

    /// <summary>W-2: receipt, completed.</summary>
    /// <param name="amount">Paid amount.</param>
    /// <param name="balance">Card balance owed now.</param>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The W-2 text.</returns>
    public static string Completed(Money amount, Money balance, PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>W-3: receipt, refused for insufficient funds.</summary>
    /// <param name="balance">Card balance owed (unchanged).</param>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The W-3 text.</returns>
    public static string RefusedInsufficientFunds(Money balance, PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>W-4: receipt, refused because the account is unavailable (also used for a not-received settlement, design §9).</summary>
    /// <param name="balance">Card balance owed (unchanged).</param>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The W-4 text.</returns>
    public static string RefusedAccountUnavailable(Money balance, PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>W-5: receipt and card view, pending.</summary>
    /// <param name="balance">Card balance owed (pending is not counted as paid).</param>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The W-5 text.</returns>
    public static string Pending(Money balance, PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>W-6: confirm refused because the balance changed.</summary>
    /// <param name="balance">The new balance owed.</param>
    /// <returns>The W-6 text.</returns>
    public static string BalanceChanged(Money balance) => throw new NotImplementedException("PR-01");

    /// <summary>W-7: card view, nothing to pay.</summary>
    /// <returns>"Nothing to pay. Your card balance is £0.00."</returns>
    public static string NothingToPay() => throw new NotImplementedException("PR-01");

    /// <summary>W-8: card view, balance above £1,000,000.00.</summary>
    /// <param name="balance">The balance owed.</param>
    /// <returns>The W-8 text.</returns>
    public static string AboveLimit(Money balance) => throw new NotImplementedException("PR-01");

    /// <summary>W-9: a payment is pending and a new one is attempted.</summary>
    /// <param name="amount">The pending payment's amount.</param>
    /// <param name="reference">The pending payment's reference.</param>
    /// <returns>The W-9 text.</returns>
    public static string PendingExists(Money amount, PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>W-10: a status check got no answer.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The W-10 text.</returns>
    public static string StillPending(PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>The receipt message for a payment's state (PLANNER-DECISION: one mapping for every receipt).</summary>
    /// <remarks>Completed → W-2(amount, balance); Failed InsufficientFunds → W-3; Failed AccountUnavailable or
    /// NotReceived → W-4; Pending → W-5. W-10 is chosen by the caller for an unanswered status check.</remarks>
    /// <param name="state">Payment state.</param>
    /// <param name="reason">Failure reason when Failed; ignored otherwise.</param>
    /// <param name="amount">Payment amount.</param>
    /// <param name="balance">Card balance owed now.</param>
    /// <param name="reference">Payment reference.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentException">Failed with a null reason.</exception>
    public static string Receipt(PaymentState state, FailureReason? reason, Money amount, Money balance, PaymentReference reference) =>
        throw new NotImplementedException("PR-01");

    /// <summary>An account or card label: <c>"{name}, ending {lastFour}"</c>.</summary>
    /// <param name="name">Display name, e.g. "Ops van card".</param>
    /// <param name="lastFour">Last four digits.</param>
    /// <returns>e.g. "Ops van card, ending 0001".</returns>
    public static string Label(string name, string lastFour) => throw new NotImplementedException("PR-01");
}
