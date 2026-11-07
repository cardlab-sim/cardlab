using CardLab.Domain.Authorisation;
using CardLab.Domain.Limits;
using CardLab.Domain.Ports;

namespace CardLab.Domain.Payments;

// Records exchanged with the store ports (design §5 "ICardStore additions", "IPaymentWriter", "PaymentRecord").
// Complete in PR-00.

/// <summary>The business's linked current account as CardLab stores it (R-2). The full number is never stored (NFR-3).</summary>
/// <param name="Id">Row id.</param>
/// <param name="DisplayName">e.g. "Brightline Supplies Ltd current account".</param>
/// <param name="LastFour">e.g. "7731".</param>
/// <param name="BankRef">The account's reference at the bank (the port's key).</param>
/// <param name="AuthorityBasis">Standing authority text named on every payment record (R-2, R-14).</param>
public sealed record LinkedAccount(Guid Id, string DisplayName, string LastFour, BankAccountRef BankRef, string AuthorityBasis);

/// <summary>A card's balance and its parts (R-1).</summary>
/// <param name="ApprovedPurchases">Sum of approved purchases.</param>
/// <param name="CompletedPayments">Sum of completed payments.</param>
/// <param name="Owed">The balance owed (<c>BalanceRules.Owed</c> of the two).</param>
public sealed record CardBalance(Money ApprovedPurchases, Money CompletedPayments, Money Owed);

/// <summary>A payment to create (state Pending) for the locked card.</summary>
/// <param name="Reference">The confirmed quote's reference.</param>
/// <param name="Kind">Payment kind.</param>
/// <param name="Amount">The quoted amount.</param>
/// <param name="Fee">The quoted fee.</param>
/// <param name="CreatedAtUtc">Acceptance time (confirm pressed), UTC.</param>
public sealed record NewPayment(PaymentReference Reference, PaymentKind Kind, Money Amount, Money Fee, DateTimeOffset CreatedAtUtc);

/// <summary>How a pending payment settled (R-6, R-11, R-14, AC-36).</summary>
/// <param name="State">Completed or Failed.</param>
/// <param name="Reason">Failure reason when Failed.</param>
/// <param name="SettledAtUtc">When CardLab recorded the outcome.</param>
/// <param name="EffectiveAtUtc">Bank debit time when reported, else <paramref name="SettledAtUtc"/> (AC-34, AC-38).</param>
/// <param name="BankDebitedAtUtc">The bank's debit time when reported.</param>
/// <param name="BalanceBefore">Balance owed immediately before the settled amount is applied.</param>
/// <param name="BalanceAfter">Balance owed immediately after (equal to before when Failed).</param>
/// <param name="Simulated">The outcome came from the fake bank's steering (R-15).</param>
public sealed record Settlement(
    PaymentState State,
    FailureReason? Reason,
    DateTimeOffset SettledAtUtc,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset? BankDebitedAtUtc,
    Money BalanceBefore,
    Money BalanceAfter,
    bool Simulated);

/// <summary>A stored payment.</summary>
/// <param name="Id">Store-assigned id.</param>
/// <param name="Reference">Its quote's reference (unique: at most one payment per reference, R-7).</param>
/// <param name="State">Current state.</param>
/// <param name="Amount">Amount.</param>
/// <param name="CreatedAtUtc">Acceptance time.</param>
/// <param name="Settlement">How it settled; null while Pending.</param>
public sealed record PaymentSnapshot(PaymentId Id, PaymentReference Reference, PaymentState State, Money Amount, DateTimeOffset CreatedAtUtc, Settlement? Settlement);

/// <summary>A stored quote with its status and payment.</summary>
/// <param name="Quote">The shown-and-accepted part.</param>
/// <param name="Status">Quote status.</param>
/// <param name="Payment">The payment for this reference, or null.</param>
public sealed record QuoteRecord(Quote Quote, QuoteStatus Status, PaymentSnapshot? Payment);

/// <summary>One append-only event against a reference (R-14).</summary>
/// <param name="Reference">The reference.</param>
/// <param name="Payment">The payment, when one exists.</param>
/// <param name="AtUtc">When, UTC.</param>
/// <param name="Kind">What happened.</param>
/// <param name="From">State before, for state changes (null for none → pending's "from").</param>
/// <param name="To">State after, for state changes.</param>
/// <param name="Detail">Optional detail: refusal code (<c>balance_changed</c>), exception type, <c>settle_lock_timeout</c>. Never a number beyond the last four.</param>
/// <param name="Simulated">The bank outcome came from steering.</param>
public sealed record PaymentEvent(PaymentReference Reference, PaymentId? Payment, DateTimeOffset AtUtc, PaymentEventKind Kind, PaymentState? From, PaymentState? To, string? Detail, bool Simulated);

/// <summary>The full record of one reference (AC-20): quote, status, refusal, payment and every event.</summary>
/// <param name="Quote">The shown-and-accepted part (never changes).</param>
/// <param name="Status">Quote status.</param>
/// <param name="RefusalReason">Refusal code (e.g. <c>balance_changed</c>) when Refused.</param>
/// <param name="BalanceAtRefusal">Balance owed when refused.</param>
/// <param name="Payment">The payment, when one exists.</param>
/// <param name="Events">Events oldest first.</param>
public sealed record PaymentRecord(Quote Quote, QuoteStatus Status, string? RefusalReason, Money? BalanceAtRefusal, PaymentSnapshot? Payment, IReadOnlyList<PaymentEvent> Events);

/// <summary>One card activity row: a purchase or a payment (R-13, DEC-12).</summary>
/// <remarks>PLANNER-DECISION: design §5 has <c>string? Outcome</c>; the record carries typed purchase fields (so the
/// admin Activity table keeps 001's category and reason columns) and the payment fields the §7 wire shape needs
/// (state, origin).</remarks>
/// <param name="At">The shown time. Purchases: the processor timestamp <c>OccurredAt</c> (001 AC-38); payments: <c>CreatedAtUtc</c>. Rows are ordered newest first by decision time (<c>DecidedAtUtc</c>) for purchases and confirm time for payments, not by <c>At</c>.</param>
/// <param name="Kind">Purchase or payment.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Description">Purchases: the merchant; payments: <c>"Payment {reference}"</c>.</param>
/// <param name="PurchaseOutcome">Purchases: approved/declined; payments: null.</param>
/// <param name="PurchaseReason">Purchases: the reason code; payments: null.</param>
/// <param name="PurchaseCategory">Purchases: the category; payments: null.</param>
/// <param name="PaymentState">Payments: the state; purchases: null.</param>
/// <param name="Reference">Payments: the reference; purchases: null.</param>
/// <param name="Origin">Payments: the quote's origin; purchases: null.</param>
public sealed record ActivityEntry(
    DateTimeOffset At,
    ActivityKind Kind,
    Money Amount,
    string Description,
    Outcome? PurchaseOutcome,
    Reason? PurchaseReason,
    Category? PurchaseCategory,
    PaymentState? PaymentState,
    PaymentReference? Reference,
    PaymentOrigin? Origin);
