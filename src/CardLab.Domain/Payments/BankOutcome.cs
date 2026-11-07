namespace CardLab.Domain.Payments;

/// <summary>
/// What the bank said about a debit (design §5), from <c>RequestDebitAsync</c> or <c>GetDebitStatusAsync</c>.
/// <see cref="Simulated"/> is true when the fake bank's steering control produced the outcome (R-15, AC-33).
/// </summary>
/// <remarks>Complete in PR-00. Closed hierarchy.</remarks>
/// <param name="Simulated">True when produced under a steering setting.</param>
public abstract record BankOutcome(bool Simulated)
{
    /// <summary>The debit was applied.</summary>
    /// <param name="DebitedAtUtc">The bank's debit time when it reports one (effective time, AC-34); null otherwise (AC-38).</param>
    /// <param name="Simulated">Steered outcome.</param>
    public sealed record Completed(DateTimeOffset? DebitedAtUtc, bool Simulated) : BankOutcome(Simulated);

    /// <summary>The bank refused the debit; nothing moved.</summary>
    /// <param name="Reason"><see cref="FailureReason.InsufficientFunds"/> or <see cref="FailureReason.AccountUnavailable"/>.</param>
    /// <param name="Simulated">Steered outcome.</param>
    public sealed record Refused(FailureReason Reason, bool Simulated) : BankOutcome(Simulated);

    /// <summary>Status check only: the bank never received a debit with this reference.</summary>
    /// <param name="Simulated">Steered outcome.</param>
    public sealed record NotReceived(bool Simulated) : BankOutcome(Simulated);

    /// <summary>No answer within the budget (or the adapter failed): the payment stays pending.</summary>
    /// <param name="Simulated">Steered outcome.</param>
    public sealed record Unknown(bool Simulated) : BankOutcome(Simulated);
}
