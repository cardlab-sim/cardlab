using System.Diagnostics;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
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
/// <see cref="PaymentOptions.BankTimeoutSeconds"/> (its own <c>CancellationTokenSource</c>, not linked to the request
/// token, BUG-002; only the budget firing, or an adapter failure, means "unanswered"), lock #2 settles with <c>CancellationToken.None</c>.
/// Logs <c>PaymentStateChanged</c> once per state change (incl. none → pending), <c>PaymentConfirmAttempt</c> per
/// confirm and <c>BankCallUnanswered</c> per unanswered call (design §12). Actor = the card's cardholder (DEC-9). No
/// <c>AuditEvent</c> rows (DEC-1).
/// </remarks>
public sealed class PaymentService
{
    private const string NoneState = "none";
    private const string NoReason = "-";

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
    public async Task<Result<Quote, PaymentError>> QuoteAsync(CardId cardId, PaymentOrigin origin, CancellationToken ct = default)
    {
        var card = await _store.GetCardAsync(cardId, ct);
        if (card is null)
        {
            return Result<Quote, PaymentError>.Fail(new PaymentError.CardNotFound());
        }

        var account = await RequireAccountAsync(ct);
        var now = Now();
        return await _store.WithCardLockAsync(
            cardId,
            async writer =>
            {
                var payments = writer.Payments;
                var owed = await OwedAsync(payments, ct);
                var pending = await payments.GetPendingPaymentAsync(ct);
                switch (BalanceRules.Availability(owed, pending is not null))
                {
                    case PayAvailability.NothingToPay:
                        return Result<Quote, PaymentError>.Fail(new PaymentError.NothingToPay(WordingV1.NothingToPay(), owed));
                    case PayAvailability.AboveLimit:
                        return Result<Quote, PaymentError>.Fail(new PaymentError.AboveLimit(WordingV1.AboveLimit(owed), owed));
                    case PayAvailability.PaymentPending:
                        return Result<Quote, PaymentError>.Fail(PendingError(pending!, owed));
                }

                var quote = QuoteRules.Create(
                    _references.Next(),
                    new CardRef(cardId, card.LastFour),
                    card.Name,
                    account,
                    owed,
                    new PaymentActor(ActorKind.Cardholder, cardId.Value, card.CardholderName),
                    origin,
                    now);
                payments.AppendQuote(quote);
                return Result<Quote, PaymentError>.Ok(quote);
            },
            ct);
    }

    /// <summary>Confirms a reference: exactly once, one in flight per card, bank called outside the lock (R-5..R-11).</summary>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Reference from the review screen.</param>
    /// <param name="ct">Request cancellation. It applies only until the payment is created in lock #1: after that a client
    /// disconnect never cancels the debit and steps 2-3 run to completion (BUG-002); a joiner's own token only stops its wait.</param>
    /// <returns>The receipt (Completed W-2 / Failed W-3, W-4 / Pending W-5; <c>Repeated</c> for a repeat), or
    /// CardNotFound / ReferenceNotFound / BalanceChanged / QuoteRefused / PaymentPending.</returns>
    public async Task<Result<Receipt, PaymentError>> ConfirmAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var card = await _store.GetCardAsync(cardId, ct);
        if (card is null)
        {
            return Result<Receipt, PaymentError>.Fail(new PaymentError.CardNotFound());
        }

        var account = await RequireAccountAsync(ct);
        var cardRef = new CardRef(cardId, card.LastFour);
        var completion = new TaskCompletionSource<Receipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registered = false;

        Validated step;
        try
        {
            step = await _store.WithCardLockAsync(
                cardId,
                writer => ValidateAsync(writer.Payments, cardId, reference, completion, () => registered = true, ct),
                ct);
        }
        catch (Exception error)
        {
            if (registered)
            {
                _inFlight.Fail(reference, completion, error);
            }

            throw;
        }

        // On the Create path the log runs inside the try below: the reference is registered by now, so nothing that can
        // throw may sit between the registration and the try that releases it (F6a).
        if (step.Attempt is not null && step.Created is null)
        {
            CardLabEvents.PaymentConfirmAttempt(_logger, reference.Value, cardId.Value.ToString(), cardRef.LastFour, step.Attempt);
        }

        if (step.Error is not null)
        {
            return Result<Receipt, PaymentError>.Fail(step.Error);
        }

        if (step.Created is null)
        {
            if (step.Join is null)
            {
                return Result<Receipt, PaymentError>.Ok(step.Stored!);
            }

            try
            {
                return Result<Receipt, PaymentError>.Ok((await step.Join.WaitAsync(ct)) with { Repeated = true });
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // The first caller's confirm failed after the debit was requested: answer from the stored state
                // (still Pending; the next status check settles it) instead of rethrowing someone else's fault.
                var stored = await FindPaymentAsync(cardId, reference, ct);
                return Result<Receipt, PaymentError>.Ok((await StoredReceiptAsync(stored!, ct)) with { Repeated = true });
            }
        }

        Receipt result;
        try
        {
            CardLabEvents.PaymentConfirmAttempt(_logger, reference.Value, cardId.Value.ToString(), cardRef.LastFour, step.Attempt!);
            CardLabEvents.PaymentStateChanged(_logger, step.Created.Id.Value.ToString(), cardId.Value.ToString(), cardRef.LastFour, PaymentWireNames.ToWire(step.Quote!.Kind), step.Created.Amount.ToString(), NoneState, PaymentWireNames.ToWire(PaymentState.Pending), NoReason, ElapsedMs(started));
            var outcome = await RequestDebitAsync(account, step.Quote);
            result = await SettleConfirmAsync(cardRef, step.Quote, step.Created, step.Owed, outcome, started);
        }
        catch (Exception error)
        {
            _inFlight.Fail(reference, completion, error);
            throw;
        }

        _inFlight.Complete(reference, completion, result);
        return Result<Receipt, PaymentError>.Ok(result);
    }

    /// <summary>Asks the bank for a pending payment's outcome and settles it (R-11; flow 2). No-op for a settled payment.</summary>
    /// <remarks>A confirm of the same reference that is still in flight is awaited first, so a check never asks the bank
    /// about a debit that has not been answered yet; the wait is bounded by the check's own budget (BUG-003): on expiry
    /// the check records <c>StatusCheckUnanswered</c>, stays Pending and answers W-10 without calling the bank.
    /// Never re-sends the debit.</remarks>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="ct">Request cancellation.</param>
    /// <returns>The receipt (W-10 when unanswered), or CardNotFound / ReferenceNotFound (no payment for the reference).</returns>
    public async Task<Result<Receipt, PaymentError>> CheckStatusAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var card = await _store.GetCardAsync(cardId, ct);
        if (card is null)
        {
            return Result<Receipt, PaymentError>.Fail(new PaymentError.CardNotFound());
        }

        var record = await FindPaymentAsync(cardId, reference, ct);
        if (record is null)
        {
            return Result<Receipt, PaymentError>.Fail(new PaymentError.ReferenceNotFound());
        }

        var waitExpired = false;
        if (_inFlight.TryGet(reference, out var inFlight))
        {
            // Never ask the bank while the debit call is open (R-9); the wait is bounded by this check's own budget (BUG-003).
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(_options.BankTimeoutSeconds));
            try
            {
                await inFlight.WaitAsync(budget.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && budget.IsCancellationRequested)
            {
                waitExpired = true;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // The first caller failed; carry on with the stored state.
            }

            record = await FindPaymentAsync(cardId, reference, ct);
            if (record is null)
            {
                return Result<Receipt, PaymentError>.Fail(new PaymentError.ReferenceNotFound());
            }
        }

        if (record.Payment!.State != PaymentState.Pending)
        {
            return Result<Receipt, PaymentError>.Ok(await StoredReceiptAsync(record, ct));
        }

        BankOutcome outcome;
        string? detail;
        if (waitExpired)
        {
            (outcome, detail) = (new BankOutcome.Unknown(false), null);
        }
        else
        {
            var account = await RequireAccountAsync(ct);
            (outcome, detail) = await AskBankAsync(budget => _bank.GetDebitStatusAsync(account.BankRef, reference, budget));
        }

        var cardRef = new CardRef(cardId, card.LastFour);
        var settled = await _store.WithCardLockAsync(
            cardId,
            writer => SettleAsync(writer.Payments, reference, outcome, detail, fromCheck: true, ct: CancellationToken.None),
            CancellationToken.None);
        // An expired wait on an in-flight confirm asked no bank, so there is no unanswered bank call to log (F9).
        LogSettled(cardRef, settled, "status", started, logUnanswered: !waitExpired);
        return Result<Receipt, PaymentError>.Ok(settled.Receipt);
    }

    /// <summary>Reads the receipt of a payment (receipt page reload); no bank call, no writes.</summary>
    /// <param name="cardId">Card.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The receipt, or CardNotFound / ReferenceNotFound.</returns>
    public async Task<Result<Receipt, PaymentError>> GetReceiptAsync(CardId cardId, PaymentReference reference, CancellationToken ct = default)
    {
        if (await _store.GetCardAsync(cardId, ct) is null)
        {
            return Result<Receipt, PaymentError>.Fail(new PaymentError.CardNotFound());
        }

        var record = await FindPaymentAsync(cardId, reference, ct);
        return record is null
            ? Result<Receipt, PaymentError>.Fail(new PaymentError.ReferenceNotFound())
            : Result<Receipt, PaymentError>.Ok(await StoredReceiptAsync(record, ct));
    }

    /// <summary>Builds the receipt of a payment (shared with the cardholder view).</summary>
    /// <param name="quote">The quote the payment was made from (labels, fee, origin).</param>
    /// <param name="payment">The payment.</param>
    /// <param name="pendingBalance">The card's current owed, shown only while the payment is Pending. A settled payment always shows its settlement's BalanceAfter, however the receipt is rebuilt (BUG-012).</param>
    /// <param name="repeated">True for a repeated confirm.</param>
    /// <param name="unansweredCheck">True after a status check the bank did not answer (W-10 instead of W-5).</param>
    /// <returns>The receipt.</returns>
    internal static Receipt BuildReceipt(Quote quote, PaymentSnapshot payment, Money pendingBalance, bool repeated, bool unansweredCheck = false)
    {
        var balanceOwed = payment.Settlement?.BalanceAfter ?? pendingBalance;
        var reason = payment.Settlement?.Reason;
        var message = unansweredCheck && payment.State == PaymentState.Pending
            ? WordingV1.StillPending(payment.Reference)
            : WordingV1.Receipt(payment.State, reason, payment.Amount, balanceOwed, payment.Reference);
        return new Receipt(
            payment.Reference,
            payment.State,
            payment.Amount,
            quote.Fee,
            payment.Settlement?.EffectiveAtUtc ?? payment.CreatedAtUtc,
            quote.FromLabel,
            quote.ToLabel,
            reason,
            message,
            balanceOwed,
            repeated,
            quote.Origin);
    }

    private static PaymentError PendingError(PaymentSnapshot pending, Money owed) =>
        new PaymentError.PaymentPending(WordingV1.PendingExists(pending.Amount, pending.Reference), pending.Reference, pending.Amount, owed);

    private static async Task<Money> OwedAsync(IPaymentWriter payments, CancellationToken ct)
    {
        var purchases = await payments.SumApprovedPurchasesAsync(ct);
        var completed = await payments.SumCompletedPaymentsAsync(ct);
        return BalanceRules.Owed(purchases, completed);
    }

    // Every instant that reaches a receipt or record is cut to milliseconds at its source: the store keeps milliseconds,
    // so a finer first receipt would differ from every repeat and read on the wire (BUG-008).
    private DateTimeOffset Now() => TruncateToMilliseconds(_clock.UtcNow);

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset instant) =>
        instant.AddTicks(-(instant.Ticks % TimeSpan.TicksPerMillisecond));

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private async Task<LinkedAccount> RequireAccountAsync(CancellationToken ct) =>
        await _store.GetLinkedAccountAsync(ct) ?? throw new InvalidOperationException("No linked current account is configured.");

    private async Task<PaymentRecord?> FindPaymentAsync(CardId cardId, PaymentReference reference, CancellationToken ct)
    {
        var record = await _store.GetPaymentRecordAsync(reference, ct);
        return record is { Payment: not null } && record.Quote.Card == cardId ? record : null;
    }

    private async Task<Receipt> StoredReceiptAsync(PaymentRecord record, CancellationToken ct)
    {
        var payment = record.Payment!;
        var pendingBalance = payment.Settlement is null ? (await _store.GetBalanceAsync(record.Quote.Card, ct))!.Owed : Money.Zero;
        return BuildReceipt(record.Quote, payment, pendingBalance, repeated: false);
    }

    private async Task<Validated> ValidateAsync(IPaymentWriter payments, CardId cardId, PaymentReference reference, TaskCompletionSource<Receipt> completion, Action markRegistered, CancellationToken ct)
    {
        var record = await payments.GetQuoteAsync(reference, ct);
        if (record is null || record.Quote.Card != cardId)
        {
            return Validated.Failed(new PaymentError.ReferenceNotFound(), null);
        }

        var now = Now();
        var owed = await OwedAsync(payments, ct);
        var pending = await payments.GetPendingPaymentAsync(ct);
        var other = pending is not null && pending.Reference != reference ? new PendingPaymentRef(pending.Reference, pending.Amount) : null;
        switch (ConfirmRules.Decide(record.Quote, record.Status, record.Payment?.State, other, owed))
        {
            case ConfirmDecision.Create:
                var created = payments.CreatePayment(new NewPayment(reference, PaymentKind.Full, record.Quote.Amount, record.Quote.Fee, now));
                payments.AppendEvent(Event(reference, created, now, PaymentEventKind.ConfirmCreated, null, PaymentState.Pending, null));
                payments.AppendEvent(Event(reference, created, now, PaymentEventKind.DebitRequested, PaymentState.Pending, PaymentState.Pending, null));
                payments.SetQuoteStatus(reference, QuoteStatus.Confirmed, null, null, now);

                // Registered before this unit of work commits, so a confirm that sees "repeat" always finds it (DEC-5).
                _inFlight.Register(reference, completion.Task);
                markRegistered();
                var snapshot = new PaymentSnapshot(created, reference, PaymentState.Pending, record.Quote.Amount, now, null);
                return Validated.Create(record.Quote, snapshot, owed);

            case ConfirmDecision.Repeat:
                var existing = record.Payment!;
                payments.AppendEvent(Event(reference, existing.Id, now, PaymentEventKind.ConfirmRepeated, existing.State, existing.State, null));
                _inFlight.TryGet(reference, out var join);
                return Validated.Repeat(join, BuildReceipt(record.Quote, existing, owed, repeated: true));

            case ConfirmDecision.RefuseBalanceChanged changed:
                payments.SetQuoteStatus(reference, QuoteStatus.Refused, "balance_changed", changed.NewOwed, now);
                payments.AppendEvent(Event(reference, null, now, PaymentEventKind.ConfirmRefused, null, null, "balance_changed"));
                return Validated.Failed(new PaymentError.BalanceChanged(WordingV1.BalanceChanged(changed.NewOwed), changed.NewOwed), "refused_balance_changed");

            case ConfirmDecision.RefuseAlreadyRefused again:
                payments.AppendEvent(Event(reference, null, now, PaymentEventKind.ConfirmRefused, null, null, "already_refused"));
                return Validated.Failed(new PaymentError.QuoteRefused(WordingV1.BalanceChanged(again.NewOwed), again.NewOwed), "refused_already_refused");

            case ConfirmDecision.RefusePendingExists blocked:
                payments.AppendEvent(Event(reference, null, now, PaymentEventKind.ConfirmRefused, null, null, "payment_pending"));
                return Validated.Failed(
                    new PaymentError.PaymentPending(WordingV1.PendingExists(blocked.Amount, blocked.Reference), blocked.Reference, blocked.Amount, owed),
                    "refused_payment_pending");

            default:
                throw new InvalidOperationException("Unknown confirm decision.");
        }
    }

    private static PaymentEvent Event(PaymentReference reference, PaymentId? payment, DateTimeOffset at, PaymentEventKind kind, PaymentState? from, PaymentState? to, string? detail, bool simulated = false) =>
        new(reference, payment, at, kind, from, to, detail, simulated);

    private async Task<BankOutcomeWithDetail> RequestDebitAsync(LinkedAccount account, Quote quote)
    {
        var request = new DebitRequest(account.BankRef, quote.Reference, quote.Amount, "CardLab card payment " + quote.Reference.Value);
        var (outcome, detail) = await AskBankAsync(budget => _bank.RequestDebitAsync(request, budget));
        return new BankOutcomeWithDetail(outcome, detail);
    }

    /// <summary>
    /// Runs a bank call with the time budget. The budget token is NOT linked to the request token (BUG-002): a client
    /// disconnect never cancels the call. The budget firing is "unknown" with no detail; an adapter failure is "unknown"
    /// with the exception type name (design §9).
    /// </summary>
    private async Task<(BankOutcome Outcome, string? Detail)> AskBankAsync(Func<CancellationToken, Task<BankOutcome>> call)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(_options.BankTimeoutSeconds));
        try
        {
            return (await call(budget.Token), null);
        }
        catch (OperationCanceledException e) when (budget.IsCancellationRequested)
        {
            // Only a steered hang says so (SimulatedNoAnswerException); a real expiry is not simulated (BUG-010).
            return (new BankOutcome.Unknown(e is SimulatedNoAnswerException), null);
        }
        catch (Exception error)
        {
            return (new BankOutcome.Unknown(false), error.GetType().Name);
        }
    }

    private async Task<Receipt> SettleConfirmAsync(CardRef card, Quote quote, PaymentSnapshot created, Money owedAtConfirm, BankOutcomeWithDetail answer, long started)
    {
        var (outcome, detail) = answer;
        SettleResult settled;
        try
        {
            settled = await SettleWithLockAsync(card.Id, quote.Reference, outcome, detail);
        }
        catch (CardLockTimeoutException)
        {
            await Task.Delay(_options.SettleLockRetryMs, CancellationToken.None);
            try
            {
                settled = await SettleWithLockAsync(card.Id, quote.Reference, outcome, detail);
            }
            catch (CardLockTimeoutException)
            {
                await TryRecordSettleLockTimeoutAsync(card.Id, quote.Reference, created.Id);
                if (outcome is BankOutcome.Unknown)
                {
                    CardLabEvents.BankCallUnanswered(_logger, quote.Reference.Value, "debit");
                }

                return BuildReceipt(quote, created, owedAtConfirm, repeated: false);
            }
        }

        LogSettled(card, settled, "debit", started);
        return settled.Receipt;
    }

    private Task<SettleResult> SettleWithLockAsync(CardId cardId, PaymentReference reference, BankOutcome outcome, string? detail) =>
        _store.WithCardLockAsync(
            cardId,
            writer => SettleAsync(writer.Payments, reference, outcome, detail, fromCheck: false, ct: CancellationToken.None),
            CancellationToken.None);

    private async Task TryRecordSettleLockTimeoutAsync(CardId cardId, PaymentReference reference, PaymentId payment)
    {
        try
        {
            await _store.WithCardLockAsync(
                cardId,
                writer =>
                {
                    writer.Payments.AppendEvent(Event(reference, payment, Now(), PaymentEventKind.BankUnanswered, PaymentState.Pending, PaymentState.Pending, "settle_lock_timeout"));
                    return Task.FromResult(true);
                },
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Best effort only: the payment stays Pending and the next status check settles it.
        }
    }

    /// <summary>Settle step shared by confirm and status check; runs inside the card lock and never calls the bank.</summary>
    private async Task<SettleResult> SettleAsync(IPaymentWriter payments, PaymentReference reference, BankOutcome outcome, string? detail, bool fromCheck, CancellationToken ct)
    {
        var record = await payments.GetQuoteAsync(reference, ct) ?? throw new InvalidOperationException("No quote for " + reference);
        var payment = record.Payment ?? throw new InvalidOperationException("No payment for " + reference);
        var before = await OwedAsync(payments, ct);
        if (payment.State != PaymentState.Pending)
        {
            return new SettleResult(BuildReceipt(record.Quote, payment, before, repeated: false), record.Quote, payment, null, false);
        }

        var now = Now();
        var transition = PaymentStateMachine.Apply(PaymentState.Pending, outcome).Value!;
        var debitedAt = transition.BankDebitedAtUtc is { } reported ? TruncateToMilliseconds(reported) : (DateTimeOffset?)null;
        if (transition.To == PaymentState.Pending)
        {
            payments.AppendEvent(Event(reference, payment.Id, now, fromCheck ? PaymentEventKind.StatusCheckUnanswered : PaymentEventKind.BankUnanswered, PaymentState.Pending, PaymentState.Pending, detail, outcome.Simulated));
            return new SettleResult(BuildReceipt(record.Quote, payment, before, repeated: false, unansweredCheck: fromCheck), record.Quote, payment, null, true);
        }

        var after = transition.To == PaymentState.Completed ? before - payment.Amount : before;
        var settlement = new Settlement(
            transition.To,
            transition.Reason,
            now,
            debitedAt ?? now,
            debitedAt,
            before,
            after,
            outcome.Simulated);
        payments.SettlePayment(payment.Id, settlement);
        payments.AppendEvent(Event(reference, payment.Id, now, SettledEventKind(outcome, fromCheck), PaymentState.Pending, transition.To, transition.Reason is { } reason ? PaymentWireNames.ToWire(reason) : null, outcome.Simulated));
        var settled = payment with { State = transition.To, Settlement = settlement };
        return new SettleResult(BuildReceipt(record.Quote, settled, after, repeated: false), record.Quote, settled, transition, false);
    }

    private static PaymentEventKind SettledEventKind(BankOutcome outcome, bool fromCheck) =>
        outcome switch
        {
            BankOutcome.Completed => fromCheck ? PaymentEventKind.StatusCheckCompleted : PaymentEventKind.BankCompleted,
            BankOutcome.NotReceived when fromCheck => PaymentEventKind.StatusCheckNotReceived,
            _ => fromCheck ? PaymentEventKind.StatusCheckRefused : PaymentEventKind.BankRefused,
        };

    private void LogSettled(CardRef card, SettleResult settled, string call, long started, bool logUnanswered = true)
    {
        if (settled.Unanswered)
        {
            if (logUnanswered)
            {
                CardLabEvents.BankCallUnanswered(_logger, settled.Payment.Reference.Value, call);
            }

            return;
        }

        if (settled.Transition is { } transition)
        {
            CardLabEvents.PaymentStateChanged(
                _logger,
                settled.Payment.Id.Value.ToString(),
                card.Id.Value.ToString(),
                card.LastFour,
                PaymentWireNames.ToWire(settled.Quote.Kind),
                settled.Payment.Amount.ToString(),
                PaymentWireNames.ToWire(transition.From),
                PaymentWireNames.ToWire(transition.To),
                transition.Reason is { } reason ? PaymentWireNames.ToWire(reason) : NoReason,
                ElapsedMs(started));
        }
    }

    private sealed record BankOutcomeWithDetail(BankOutcome Outcome, string? Detail);

    private sealed record SettleResult(Receipt Receipt, Quote Quote, PaymentSnapshot Payment, Transition? Transition, bool Unanswered);

    private sealed record Validated(string? Attempt, PaymentError? Error, Receipt? Stored, Task<Receipt>? Join, Quote? Quote, PaymentSnapshot? Created, Money Owed)
    {
        public static Validated Failed(PaymentError error, string? attempt) => new(attempt, error, null, null, null, null, Money.Zero);

        public static Validated Repeat(Task<Receipt>? join, Receipt stored) => new("repeated", null, stored, join, null, null, Money.Zero);

        public static Validated Create(Quote quote, PaymentSnapshot created, Money owed) => new("created", null, null, null, quote, created, owed);
    }
}
