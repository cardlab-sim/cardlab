using CardLab.Domain.Controls;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;

namespace CardLab.Domain.Application;

/// <summary>
/// What the cardholder sees after a confirm, a status check or a receipt reload (R-12, design §5 and §7).
/// </summary>
/// <param name="Reference">Payment reference.</param>
/// <param name="State">Payment state.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Fee">Fee (£0.00).</param>
/// <param name="Time">Effective time when settled; acceptance time while pending (design §7).</param>
/// <param name="FromLabel">Source label as shown on the review screen.</param>
/// <param name="ToLabel">Destination label as shown on the review screen.</param>
/// <param name="Reason">Failure reason when Failed.</param>
/// <param name="Message">W-2..W-5, or W-10 after an unanswered status check (server-rendered, DEC-10).</param>
/// <param name="BalanceOwed">The card's balance owed now.</param>
/// <param name="Repeated">True when this confirm repeated an earlier one for the same reference (AC-9).</param>
/// <param name="Origin">The quote's origin (DEC-15).</param>
public sealed record Receipt(
    PaymentReference Reference,
    PaymentState State,
    Money Amount,
    Money Fee,
    DateTimeOffset Time,
    string FromLabel,
    string ToLabel,
    FailureReason? Reason,
    string Message,
    Money BalanceOwed,
    bool Repeated,
    PaymentOrigin Origin);

/// <summary>Expected failures of the payment operations (design §5). The API maps each to a status and <c>code</c> (§7).</summary>
public abstract record PaymentError
{
    private PaymentError()
    {
    }

    /// <summary>No card has the id (404).</summary>
    public sealed record CardNotFound : PaymentError;

    /// <summary>Unknown reference, another card's reference, or a reference with no payment where one is required (404).</summary>
    public sealed record ReferenceNotFound : PaymentError;

    /// <summary>Balance £0.00 at quote time (409 <c>nothing_to_pay</c>, W-7).</summary>
    /// <param name="Message">W-7.</param>
    /// <param name="BalanceOwed">£0.00.</param>
    public sealed record NothingToPay(string Message, Money BalanceOwed) : PaymentError;

    /// <summary>Balance above £1,000,000.00 at quote time (409 <c>above_limit</c>, W-8).</summary>
    /// <param name="Message">W-8.</param>
    /// <param name="BalanceOwed">The balance.</param>
    public sealed record AboveLimit(string Message, Money BalanceOwed) : PaymentError;

    /// <summary>Another payment on the card is pending (409 <c>payment_pending</c>, W-9; quote or confirm).</summary>
    /// <param name="Message">W-9.</param>
    /// <param name="PendingReference">The pending payment's reference.</param>
    /// <param name="Amount">The pending payment's amount.</param>
    /// <param name="BalanceOwed">The balance owed.</param>
    public sealed record PaymentPending(string Message, PaymentReference PendingReference, Money Amount, Money BalanceOwed) : PaymentError;

    /// <summary>The balance changed between review and confirm (409 <c>balance_changed</c>, W-6).</summary>
    /// <param name="Message">W-6 with the new balance.</param>
    /// <param name="BalanceOwed">The new balance.</param>
    public sealed record BalanceChanged(string Message, Money BalanceOwed) : PaymentError;

    /// <summary>The reference was refused before (409 <c>quote_refused</c>, W-6, AC-30).</summary>
    /// <param name="Message">W-6 with the current balance.</param>
    /// <param name="BalanceOwed">The current balance.</param>
    public sealed record QuoteRefused(string Message, Money BalanceOwed) : PaymentError;
}

/// <summary>The cardholder view aggregate (R-1..R-3, R-13, R-16; design §5 <c>CardholderViewService</c>).</summary>
/// <param name="Card">Card id + last four.</param>
/// <param name="CardName">Card name.</param>
/// <param name="CardholderName">The acting person (R-16).</param>
/// <param name="Status">Card status.</param>
/// <param name="BalanceOwed">Balance owed.</param>
/// <param name="Availability">Pay availability.</param>
/// <param name="AvailabilityMessage">W-7 / W-8 / W-9 when not Available; null when Available.</param>
/// <param name="PendingPayment">Receipt (W-5) of the pending payment, or null.</param>
/// <param name="Account">Linked account.</param>
/// <param name="AccountAvailable">Available balance from the bank.</param>
/// <param name="CardActivity">Last 10 purchases and payments, newest first.</param>
/// <param name="AccountActivity">Last 10 ledger entries, newest first.</param>
public sealed record CardholderView(
    CardRef Card,
    string CardName,
    string CardholderName,
    CardStatus Status,
    Money BalanceOwed,
    PayAvailability Availability,
    string? AvailabilityMessage,
    Receipt? PendingPayment,
    LinkedAccount Account,
    Money AccountAvailable,
    IReadOnlyList<ActivityEntry> CardActivity,
    IReadOnlyList<BankEntry> AccountActivity);

/// <summary>Payment settings (<c>CardLab:Payments</c>, design §11). Bound in <c>AddCardLabInfrastructure</c>.</summary>
/// <remarks>PLANNER-DECISION: a plain class passed to the services (no <c>IOptions</c>), so the domain project gains no
/// package reference.</remarks>
public sealed class PaymentOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "CardLab:Payments";

    /// <summary>Bank time budget per call in seconds (NFR-2): 10 in production, 1 in integration tests.</summary>
    public int BankTimeoutSeconds { get; set; } = 10;

    /// <summary>Delay before the single retry of a settle (lock #2) that timed out on the card lock (design §9).</summary>
    public int SettleLockRetryMs { get; set; } = 500;
}
