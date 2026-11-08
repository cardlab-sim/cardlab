namespace CardLab.Domain.Payments;

// Payment vocabulary (design §5, 02-design-digest "Interfaces"). Complete in PR-00: no rules here.
// Wire names live in PaymentWireNames (PR-01); enum names are what the database stores.

/// <summary>Lifecycle state of a payment (R-6): exactly one at a time; Completed and Failed are terminal.</summary>
public enum PaymentState
{
    /// <summary>Sent to the bank (or about to be); outcome not yet known. Not counted as paid (R-1, R-9).</summary>
    Pending,

    /// <summary>The bank debited the current account and the card balance is reduced.</summary>
    Completed,

    /// <summary>The bank refused or never received the debit; nothing moved.</summary>
    Failed,
}

/// <summary>What kind of payment (slice 2 adds Minimum and Chosen; never hard-code "amount = balance" in records).</summary>
public enum PaymentKind
{
    /// <summary>The full balance owed at review time (the only option in slice 1).</summary>
    Full,
}

/// <summary>Why a payment failed (R-10, R-11).</summary>
public enum FailureReason
{
    /// <summary>The bank refused: not enough money in the current account (W-3).</summary>
    InsufficientFunds,

    /// <summary>The bank refused: account unavailable / could not process (W-4).</summary>
    AccountUnavailable,

    /// <summary>A status check found the bank never received the debit (shown with W-4 text plus a "not received" badge).</summary>
    NotReceived,
}

/// <summary>Status of a quote (the reference minted when the review screen is shown, R-7).</summary>
public enum QuoteStatus
{
    /// <summary>Shown, not yet confirmed.</summary>
    Open,

    /// <summary>Confirmed: a payment exists for this reference.</summary>
    Confirmed,

    /// <summary>Confirm refused (balance changed, R-5); can never start a payment.</summary>
    Refused,
}

/// <summary>Whether the cardholder may start a full-balance payment now (R-3, R-8, R-17).</summary>
public enum PayAvailability
{
    /// <summary>Balance £0.01..£1,000,000.00 and nothing pending.</summary>
    Available,

    /// <summary>Balance £0.00 (W-7).</summary>
    NothingToPay,

    /// <summary>Balance above £1,000,000.00 (W-8).</summary>
    AboveLimit,

    /// <summary>A payment on this card is pending (W-9).</summary>
    PaymentPending,
}

/// <summary>Kind of acting person on a payment record (DEC-9).</summary>
public enum ActorKind
{
    /// <summary>The seeded admin (not used by payment actions in slice 1).</summary>
    Admin,

    /// <summary>The card's cardholder (every payment action in slice 1).</summary>
    Cardholder,
}

/// <summary>Where a quote came from (DEC-15). Stored on the quote; copied to receipts, records, lists and activity.</summary>
public enum PaymentOrigin
{
    /// <summary>A cardholder using the cardholder view (the default).</summary>
    CardholderView,

    /// <summary>The deploy smoke on <c>demo</c>: only when the quote request carries <c>X-CardLab-Origin: deploy-smoke</c> exactly.</summary>
    DeploySmoke,
}

/// <summary>Kind of a card activity row (R-13): purchases and payments merged, newest first.</summary>
public enum ActivityKind
{
    /// <summary>An authorisation decision (001 <c>Transaction</c> row).</summary>
    Purchase,

    /// <summary>A payment (any state).</summary>
    Payment,
}

/// <summary>Kinds of append-only payment events recorded against a reference (R-14, design §5).</summary>
public enum PaymentEventKind
{
    /// <summary>First confirm created the pending payment.</summary>
    ConfirmCreated,

    /// <summary>A later confirm of the same reference (double click, refresh, retry).</summary>
    ConfirmRepeated,

    /// <summary>A confirm refused (balance changed, already refused, or another payment pending).</summary>
    ConfirmRefused,

    /// <summary>The debit request was sent to the bank.</summary>
    DebitRequested,

    /// <summary>The bank answered the debit: completed.</summary>
    BankCompleted,

    /// <summary>The bank answered the debit: refused (reason in the event).</summary>
    BankRefused,

    /// <summary>The bank did not answer the debit within the budget (or the adapter threw); payment stays pending.</summary>
    BankUnanswered,

    /// <summary>A status check found the debit completed.</summary>
    StatusCheckCompleted,

    /// <summary>A status check found the debit refused.</summary>
    StatusCheckRefused,

    /// <summary>A status check found the bank never received the debit.</summary>
    StatusCheckNotReceived,

    /// <summary>A status check got no answer within the budget; payment stays pending.</summary>
    StatusCheckUnanswered,
}

/// <summary>Store-assigned identifier of a payment.</summary>
/// <param name="Value">Database key.</param>
public readonly record struct PaymentId(Guid Value);

/// <summary>Who acted on a payment (R-14, R-16, DEC-9).</summary>
/// <param name="Kind">Cardholder for every slice-1 payment action.</param>
/// <param name="Id">For a cardholder: the card id (the persona is derived from the card); for an admin: the admin id.</param>
/// <param name="Name">Display name, e.g. "Priya Nair" (never the admin's name on cardholder actions, AC-23).</param>
public sealed record PaymentActor(ActorKind Kind, Guid Id, string Name);
