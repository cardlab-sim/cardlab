using CardLab.Domain.Payments;

namespace CardLab.Domain.Ports;

/// <summary>The account's key at the bank (the port's account identifier; never the account number).</summary>
/// <param name="Value">Bank-side id.</param>
public readonly record struct BankAccountRef(Guid Value);

/// <summary>The bank's view of the linked current account.</summary>
/// <param name="Ref">Account key.</param>
/// <param name="LastFour">Last four digits of the account number (NFR-3).</param>
/// <param name="AvailableBalance">Available balance.</param>
public sealed record AccountSnapshot(BankAccountRef Ref, string LastFour, Money AvailableBalance);

/// <summary>One ledger entry on the current account (R-13: a completed payment is a debit with the same reference).</summary>
/// <param name="Reference">The payment reference.</param>
/// <param name="Amount">Debited amount.</param>
/// <param name="PostedAtUtc">When the bank posted it.</param>
/// <param name="Narrative">Bank narrative, e.g. "CardLab card payment PAY-…".</param>
public sealed record BankEntry(PaymentReference Reference, Money Amount, DateTimeOffset PostedAtUtc, string Narrative);

/// <summary>A debit request, identified by its reference (the bank is idempotent on it).</summary>
/// <param name="Account">Account to debit.</param>
/// <param name="Reference">Payment reference.</param>
/// <param name="Amount">Amount (never hard-coded to the balance; slice 2 sends other amounts).</param>
/// <param name="Narrative">Narrative for the ledger.</param>
public sealed record DebitRequest(BankAccountRef Account, PaymentReference Reference, Money Amount, string Narrative);

/// <summary>
/// The external linked current account (design §5, ADR-007). Only adapter in slice 1: <c>SqlFakeBank</c>; test double:
/// <c>FakeBank</c>. Both must pass <c>CurrentAccountBankContractTests</c>.
/// </summary>
/// <remarks>
/// Contract: a reference the bank has seen is never applied twice (a repeated debit returns the recorded outcome);
/// a debit larger than the available balance is refused with InsufficientFunds; <see cref="RequestDebitAsync"/> and
/// <see cref="GetDebitStatusAsync"/> may never return: the caller's <c>ct</c> is the time budget and, when it fires, they
/// throw <see cref="OperationCanceledException"/>. A no-answer produced by steering ends with the subclass
/// <see cref="SimulatedNoAnswerException"/> instead, so the caller can mark the outcome simulated (R-15, BUG-010); any other
/// no-answer ends with a plain <see cref="OperationCanceledException"/>. Fake-bank tables are never joined with card tables (DEC-2).
/// </remarks>
public interface ICurrentAccountBank
{
    /// <summary>Reads the account.</summary>
    /// <param name="account">Account key.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The snapshot.</returns>
    Task<AccountSnapshot> GetAccountAsync(BankAccountRef account, CancellationToken ct = default);

    /// <summary>Recent ledger entries, newest first by <c>PostedAtUtc</c>.</summary>
    /// <param name="account">Account key.</param>
    /// <param name="take">Maximum entries (10 on the cardholder view).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>Up to <paramref name="take"/> entries.</returns>
    Task<IReadOnlyList<BankEntry>> ListRecentEntriesAsync(BankAccountRef account, int take, CancellationToken ct = default);

    /// <summary>Requests a debit.</summary>
    /// <param name="request">The request.</param>
    /// <param name="ct">The time budget (10 s in production); cancellation means "no answer".</param>
    /// <returns><see cref="BankOutcome.Completed"/> or <see cref="BankOutcome.Refused"/>.</returns>
    /// <exception cref="OperationCanceledException">The bank did not answer before <paramref name="ct"/> fired
    /// (<see cref="SimulatedNoAnswerException"/> when steering caused it).</exception>
    Task<BankOutcome> RequestDebitAsync(DebitRequest request, CancellationToken ct = default);

    /// <summary>Asks the outcome of a debit by reference.</summary>
    /// <param name="account">Account key.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="ct">The time budget.</param>
    /// <returns><see cref="BankOutcome.Completed"/>, <see cref="BankOutcome.Refused"/> or <see cref="BankOutcome.NotReceived"/>.</returns>
    /// <exception cref="OperationCanceledException">The bank did not answer before <paramref name="ct"/> fired
    /// (<see cref="SimulatedNoAnswerException"/> when steering caused it).</exception>
    Task<BankOutcome> GetDebitStatusAsync(BankAccountRef account, PaymentReference reference, CancellationToken ct = default);
}

/// <summary>
/// The caller's token fired while the fake bank was hanging because of a steering setting (R-15, BUG-010): the
/// unanswered outcome is simulated. Only fakes throw it; a real no-answer is a plain <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class SimulatedNoAnswerException : OperationCanceledException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="token">The token that fired.</param>
    public SimulatedNoAnswerException(CancellationToken token)
        : base("The bank did not answer (simulated by steering).", token)
    {
    }
}

/// <summary>Mints payment references (DEC-8). Real: <c>SystemPaymentReferences</c> (40 random bits); test: sequential.</summary>
public interface IPaymentReferences
{
    /// <summary>A new reference matching <see cref="PaymentReference.Pattern"/>.</summary>
    /// <returns>The reference.</returns>
    PaymentReference Next();
}

/// <summary>
/// Payment writes and reads for the locked card, valid only inside <see cref="ICardStore.WithCardLockAsync{T}"/>
/// (DEC-4: payments share the card's unit of work). Writes commit with the unit of work.
/// </summary>
/// <remarks>Implemented by <c>SqlPaymentWriter</c> (PR-05) and the in-memory store (PR-02); contract
/// <c>PaymentStoreContractTests</c>. Sums see committed rows plus writes already flushed in this unit of work.</remarks>
public interface IPaymentWriter
{
    /// <summary>Sum of the locked card's approved purchases (001 <c>Transaction</c> rows with outcome Approved).</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The sum; £0.00 when none.</returns>
    Task<Money> SumApprovedPurchasesAsync(CancellationToken ct = default);

    /// <summary>Sum of the locked card's payments in state Completed.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The sum; £0.00 when none.</returns>
    Task<Money> SumCompletedPaymentsAsync(CancellationToken ct = default);

    /// <summary>Reads a quote of the locked card.</summary>
    /// <param name="reference">Reference.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The quote with status and payment, or null when the reference is unknown or belongs to another card.</returns>
    Task<QuoteRecord?> GetQuoteAsync(PaymentReference reference, CancellationToken ct = default);

    /// <summary>The locked card's pending payment.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The pending payment, or null (at most one exists, R-8).</returns>
    Task<PaymentSnapshot?> GetPendingPaymentAsync(CancellationToken ct = default);

    /// <summary>Stores a new quote (status Open) for the locked card.</summary>
    /// <param name="quote">The quote; its <c>Card</c> is the locked card.</param>
    void AppendQuote(Quote quote);

    /// <summary>Changes a quote's status (the only mutable quote columns, design §6).</summary>
    /// <param name="reference">Reference.</param>
    /// <param name="status">New status.</param>
    /// <param name="refusalReason">Refusal code when Refused, e.g. <c>balance_changed</c>.</param>
    /// <param name="balanceAtRefusal">Balance owed when refused.</param>
    /// <param name="atUtc">When (stored as <c>RefusedAtUtc</c> for Refused).</param>
    void SetQuoteStatus(PaymentReference reference, QuoteStatus status, string? refusalReason, Money? balanceAtRefusal, DateTimeOffset atUtc);

    /// <summary>Creates a payment in state Pending for the locked card.</summary>
    /// <param name="payment">The payment.</param>
    /// <returns>The store-assigned id.</returns>
    /// <remarks>A second pending payment on the card, or a second payment for one reference, fails on commit (unique
    /// indexes) with the store's exception; the rules prevent it first.</remarks>
    PaymentId CreatePayment(NewPayment payment);

    /// <summary>Settles a pending payment (state, reason, times, balances before/after, simulated).</summary>
    /// <param name="id">Payment id.</param>
    /// <param name="settlement">The settlement.</param>
    void SettlePayment(PaymentId id, Settlement settlement);

    /// <summary>Appends an event (append-only).</summary>
    /// <param name="paymentEvent">The event.</param>
    void AppendEvent(PaymentEvent paymentEvent);
}
