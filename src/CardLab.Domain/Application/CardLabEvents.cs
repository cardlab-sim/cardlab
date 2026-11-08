using Microsoft.Extensions.Logging;

namespace CardLab.Domain.Application;

/// <summary>
/// The structured log events of NFR-7 (DEC-12, design §12): exactly one per authorisation and one per
/// real control change, emitted by the application services so both API paths log identically.
/// </summary>
/// <remarks>
/// PLANNER-DECISION: lives in the domain's application layer (not <c>CardLab.Infrastructure/Logging</c> as
/// DEC-12 says) because the services that emit it are in <c>CardLab.Domain</c>, which cannot reference
/// Infrastructure. Complete in PR-00 (source-generated). Field values are wire names / invariant text;
/// never a card number (NFR-1).
/// </remarks>
public static partial class CardLabEvents
{
    /// <summary>One authorisation decision (any reason, including <c>invalid_request</c> and <c>card_not_found</c>).</summary>
    /// <param name="logger">Logger of the emitting service.</param>
    /// <param name="cardId">Internal card id as text; <c>"-"</c> when no card was resolved.</param>
    /// <param name="lastFour">Last four digits; <c>"-"</c> when no card was resolved.</param>
    /// <param name="category">Category wire name; <c>"-"</c> when the request's category was invalid.</param>
    /// <param name="amount">Amount as <c>Money.ToString()</c>; <c>"-"</c> when the amount was invalid.</param>
    /// <param name="outcome">Outcome wire name.</param>
    /// <param name="reason">Reason wire name.</param>
    /// <param name="durationMs">Elapsed milliseconds from service entry to decision.</param>
    [LoggerMessage(EventId = 1001, EventName = "AuthorisationDecided", Level = LogLevel.Information,
        Message = "Authorisation decided: card {CardId} ({LastFour}) {Category} {Amount} -> {Outcome} {Reason} in {DurationMs} ms")]
    public static partial void AuthorisationDecided(ILogger logger, string cardId, string lastFour, string category, string amount, string outcome, string reason, long durationMs);

    /// <summary>One real control change (no-ops are not logged).</summary>
    /// <param name="logger">Logger of the emitting service.</param>
    /// <param name="actorId">Admin id as text.</param>
    /// <param name="actorName">Admin display name.</param>
    /// <param name="cardId">Internal card id as text.</param>
    /// <param name="lastFour">Last four digits.</param>
    /// <param name="action">Audit action wire name, e.g. <c>limit_changed</c>.</param>
    [LoggerMessage(EventId = 1002, EventName = "ControlChanged", Level = LogLevel.Information,
        Message = "Control changed: {Action} on card {CardId} ({LastFour}) by {ActorName} ({ActorId})")]
    public static partial void ControlChanged(ILogger logger, string actorId, string actorName, string cardId, string lastFour, string action);

    /// <summary>One payment state change, including none → pending (002 NFR-4, design §12). Not logged for "unanswered".</summary>
    /// <param name="logger">Logger of <c>PaymentService</c>.</param>
    /// <param name="paymentId">Payment id as text.</param>
    /// <param name="cardId">Card id as text.</param>
    /// <param name="lastFour">Card last four.</param>
    /// <param name="kind">Payment kind wire name (<c>full</c>).</param>
    /// <param name="amount">Amount as <c>Money.ToString()</c>.</param>
    /// <param name="fromState">State wire name before, or <c>"none"</c> on creation.</param>
    /// <param name="toState">State wire name after.</param>
    /// <param name="reason">Failure reason wire name, or <c>"-"</c>.</param>
    /// <param name="durationMs">Milliseconds from service entry to the change.</param>
    [LoggerMessage(EventId = 1003, EventName = "PaymentStateChanged", Level = LogLevel.Information,
        Message = "Payment state changed: {PaymentId} card {CardId} ({LastFour}) {Kind} {Amount} {FromState} -> {ToState} {Reason} in {DurationMs} ms")]
    public static partial void PaymentStateChanged(ILogger logger, string paymentId, string cardId, string lastFour, string kind, string amount, string fromState, string toState, string reason, long durationMs);

    /// <summary>One confirm attempt (design §12).</summary>
    /// <param name="logger">Logger of <c>PaymentService</c>.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="cardId">Card id as text.</param>
    /// <param name="lastFour">Card last four.</param>
    /// <param name="result"><c>created</c> | <c>repeated</c> | <c>refused_balance_changed</c> | <c>refused_already_refused</c> | <c>refused_payment_pending</c>.</param>
    [LoggerMessage(EventId = 1004, EventName = "PaymentConfirmAttempt", Level = LogLevel.Information,
        Message = "Payment confirm attempt: {Reference} card {CardId} ({LastFour}) -> {Result}")]
    public static partial void PaymentConfirmAttempt(ILogger logger, string reference, string cardId, string lastFour, string result);

    /// <summary>The bank did not answer within the budget (design §12).</summary>
    /// <param name="logger">Logger of <c>PaymentService</c>.</param>
    /// <param name="reference">Payment reference.</param>
    /// <param name="call"><c>debit</c> | <c>status</c>.</param>
    [LoggerMessage(EventId = 1005, EventName = "BankCallUnanswered", Level = LogLevel.Warning,
        Message = "Bank call unanswered: {Call} for {Reference}")]
    public static partial void BankCallUnanswered(ILogger logger, string reference, string call);
}
