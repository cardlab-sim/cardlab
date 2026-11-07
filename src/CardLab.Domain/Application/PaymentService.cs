using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace CardLab.Domain.Application;

/// <summary>
/// Cardholder payment operations (R-3..R-12, R-14; DEC-3 three-step confirm, DEC-5 in-flight join, ADR-006).
/// </summary>
/// <remarks>
/// Rules come from <c>CardLab.Domain.Payments</c>; this class only orchestrates ports. The bank is never called inside
/// the card lock: lock #1 validates and creates the pending payment, the bank call runs unlocked with a budget of
/// <see cref="PaymentOptions.BankTimeoutSeconds"/> (a <c>CancellationTokenSource</c> linked to the request token; only
/// the budget firing means "unanswered"), lock #2 settles with <c>CancellationToken.None</c>. Logs
/// <c>PaymentStateChanged</c> once per state change (incl. none → pending), <c>PaymentConfirmAttempt</c> per confirm and
/// <c>BankCallUnanswered</c> per unanswered call (design §12). Actor = the card's cardholder (DEC-9). No
/// <c>AuditEvent</c> rows (DEC-1). Implemented in PR-04.
/// </remarks>
public sealed class PaymentService
{
    private readonly ICardStore _store;
    private readonly ICurrentAccountBank _bank;
    private readonly IPaymentReferences _references;
    private readonly IClock _clock;
    private readonly InFlightPayments _inFlight;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="store">Card store (payments share its unit of work, DEC-4).</param>
    /// <param name="bank">The linked current account.</param>
    /// <param name="references">Reference source.</param>
    /// <param name="clock">Clock for quote, acceptance and settle times.</param>
    /// <param name="inFlight">Singleton in-flight registry (DEC-5).</param>
    /// <param name="options">Bank budget and settle retry.</param>
    /// <param name="logger">Logger for <see cref="CardLabEvents"/>.</param>
    public PaymentService(ICardStore store, ICurrentAccountBank bank, IPaymentReferences references, IClock clock, InFlightPayments inFlight, PaymentOptions options, ILogger<PaymentService> logger)
    {
        _store = store;
        _bank = bank;
        _references = references;
        _clock = clock;
        _inFlight = inFlight;
        _options = options;
        _logger = logger;
    }

    /// <summary>Mints a reference and stores the full-balance quote shown on the review screen (R-3, R-4, R-14).</summary>
    /// <remarks>Inside the card lock: sums → <c>BalanceRules.Owed</c>; pending payment → <c>BalanceRules.Availability</c>;
    /// not Available → error, nothing stored; else <c>QuoteRules.Create</c> + <c>AppendQuote</c>.</remarks>
    /// <param name="cardId">Card.</param>
    /// <param name="origin">Origin from the request header (DEC-15).</param>
    /// <param name="ct">Request cancellation.</param>
    /// <returns>The quote, or CardNotFound / NothingToPay (W-7) / AboveLimit (W-8) / PaymentPending (W-9).</returns>
    public Task<Result<Quote, PaymentError>> QuoteAsync(CardId cardId, PaymentOrigin origin, CancellationToken ct = default) =>
        throw new NotImplementedException("PR-04");

    /// <summary>Confirms a reference: exactly once, one in flight per card, bank called outside the lock (R-5..R-11).</summary>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Reference from the review screen.</param>
    /// <param name="ct">Request cancellation (a client disconnect never marks the payment unanswered).</param>
    /// <returns>The receipt (Completed W-2 / Failed W-3, W-4 / Pending W-5; <c>Repeated</c> for a repeat), or
    /// CardNotFound / ReferenceNotFound / BalanceChanged / QuoteRefused / PaymentPending.</returns>
    public Task<Result<Receipt, PaymentError>> ConfirmAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default) =>
        throw new NotImplementedException("PR-04");

    /// <summary>Asks the bank for a pending payment's outcome and settles it (R-11; flow 2). No-op for a settled payment.</summary>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="ct">Request cancellation.</param>
    /// <returns>The receipt (W-10 when unanswered), or CardNotFound / ReferenceNotFound (no payment for the reference).</returns>
    public Task<Result<Receipt, PaymentError>> CheckStatusAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default) =>
        throw new NotImplementedException("PR-04");

    /// <summary>Reads the receipt of a payment (receipt page reload); no bank call, no writes.</summary>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The receipt, or CardNotFound / ReferenceNotFound.</returns>
    public Task<Result<Receipt, PaymentError>> GetReceiptAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default) =>
        throw new NotImplementedException("PR-04");
}
