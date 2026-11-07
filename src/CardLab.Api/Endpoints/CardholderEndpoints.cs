using CardLab.Api.Contracts;
using CardLab.Domain;
using CardLab.Domain.Application;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;

namespace CardLab.Api.Endpoints;

/// <summary>
/// Cardholder endpoints under <c>/api/cardholder/cards/{id}</c> (002 design §7, R-1..R-16). No login (decision 9); the
/// actor is the card's cardholder (DEC-9). Thin: parse route → <see cref="PaymentService"/> /
/// <see cref="CardholderViewService"/> → <see cref="PaymentDtoMapper"/>.
/// </summary>
/// <remarks>
/// Errors (PR-06): unknown card, unknown / other card's / malformed reference (<c>PaymentReference.TryParse</c>) → 404
/// ProblemDetails; <see cref="PaymentError"/> refusals → 409 ProblemDetails with extensions <c>code</c>, <c>message</c>,
/// <c>balanceOwed</c> (+ <c>pendingReference</c>, <c>amount</c> for <c>payment_pending</c>). Lock timeout / SQL failure →
/// 503 via <c>CardLabExceptionHandler</c>. Route table complete in PR-00; handler bodies implemented in PR-06.
/// </remarks>
public static class CardholderEndpoints
{
    /// <summary>The optional request header that marks a deploy-smoke quote (DEC-15).</summary>
    public const string OriginHeader = "X-CardLab-Origin";

    /// <summary>Maps the cardholder routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapCardholderEndpoints(this IEndpointRouteBuilder app)
    {
        var cards = app.MapGroup("/api/cardholder/cards");
        cards.MapGet("/{id:guid}", GetView);
        cards.MapPost("/{id:guid}/quotes", CreateQuote);
        cards.MapPost("/{id:guid}/payments/{reference}/confirm", Confirm);
        cards.MapPost("/{id:guid}/payments/{reference}/check", Check);
        cards.MapGet("/{id:guid}/payments/{reference}", GetReceipt);
        return app;
    }

    /// <summary><c>GET /api/cardholder/cards/{id}</c> → 200 <see cref="CardholderViewDto"/> | 404. Pure read (DEC-13).</summary>
    internal static async Task<IResult> GetView(Guid id, CardholderViewService views, CancellationToken ct)
    {
        var result = await views.GetAsync(new CardId(id), ct);
        return result.IsOk ? TypedResults.Ok(PaymentDtoMapper.ToDto(result.Value!)) : ToProblem(result.Error!);
    }

    /// <summary><c>POST .../quotes</c> (header <see cref="OriginHeader"/> optional) → 201 <see cref="QuoteDto"/> | 409 nothing_to_pay / above_limit / payment_pending | 404.</summary>
    internal static async Task<IResult> CreateQuote(Guid id, HttpRequest request, PaymentService payments, CancellationToken ct)
    {
        var origin = PaymentWireNames.OriginFromHeader(request.Headers[OriginHeader].FirstOrDefault());
        var result = await payments.QuoteAsync(new CardId(id), origin, ct);
        return result.IsOk ? TypedResults.Created((string?)null, PaymentDtoMapper.ToDto(result.Value!)) : ToProblem(result.Error!);
    }

    /// <summary><c>POST .../payments/{reference}/confirm</c> → 200 <see cref="ReceiptDto"/> | 409 balance_changed / quote_refused / payment_pending | 404.</summary>
    internal static async Task<IResult> Confirm(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        PaymentReference.TryParse(reference, out var parsed)
            ? ToResult(await payments.ConfirmAsync(new CardId(id), parsed, ct))
            : ReferenceNotFound();

    /// <summary><c>POST .../payments/{reference}/check</c> → 200 <see cref="ReceiptDto"/> (W-10 when unanswered) | 404.</summary>
    internal static async Task<IResult> Check(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        PaymentReference.TryParse(reference, out var parsed)
            ? ToResult(await payments.CheckStatusAsync(new CardId(id), parsed, ct))
            : ReferenceNotFound();

    /// <summary><c>GET .../payments/{reference}</c> → 200 <see cref="ReceiptDto"/> | 404.</summary>
    internal static async Task<IResult> GetReceipt(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        PaymentReference.TryParse(reference, out var parsed)
            ? ToResult(await payments.GetReceiptAsync(new CardId(id), parsed, ct))
            : ReferenceNotFound();

    private static IResult ToResult(Result<Receipt, PaymentError> result) =>
        result.IsOk ? TypedResults.Ok(PaymentDtoMapper.ToDto(result.Value!)) : ToProblem(result.Error!);

    private static IResult ReferenceNotFound() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Payment not found");

    private static IResult ToProblem(PaymentError error) => error switch
    {
        PaymentError.NothingToPay e => Conflict("nothing_to_pay", e.Message, e.BalanceOwed),
        PaymentError.AboveLimit e => Conflict("above_limit", e.Message, e.BalanceOwed),
        PaymentError.BalanceChanged e => Conflict("balance_changed", e.Message, e.BalanceOwed),
        PaymentError.QuoteRefused e => Conflict("quote_refused", e.Message, e.BalanceOwed),
        PaymentError.PaymentPending e => Conflict("payment_pending", e.Message, e.BalanceOwed, e.PendingReference, e.Amount),
        PaymentError.CardNotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Card not found"),
        _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Payment not found"),
    };

    private static IResult Conflict(string code, string message, Money balanceOwed, PaymentReference? pending = null, Money? amount = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["message"] = message,
            ["balanceOwed"] = balanceOwed.ToString(),
        };
        if (pending is not null)
        {
            extensions["pendingReference"] = pending.Value.Value;
            extensions["amount"] = amount!.Value.ToString();
        }

        return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Payment refused", extensions: extensions);
    }
}
