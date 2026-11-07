using CardLab.Api.Contracts;
using CardLab.Domain.Application;

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
    internal static Task<IResult> GetView(Guid id, CardholderViewService views, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");

    /// <summary><c>POST .../quotes</c> (header <see cref="OriginHeader"/> optional) → 201 <see cref="QuoteDto"/> | 409 nothing_to_pay / above_limit / payment_pending | 404.</summary>
    internal static Task<IResult> CreateQuote(Guid id, HttpRequest request, PaymentService payments, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");

    /// <summary><c>POST .../payments/{reference}/confirm</c> → 200 <see cref="ReceiptDto"/> | 409 balance_changed / quote_refused / payment_pending | 404.</summary>
    internal static Task<IResult> Confirm(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");

    /// <summary><c>POST .../payments/{reference}/check</c> → 200 <see cref="ReceiptDto"/> (W-10 when unanswered) | 404.</summary>
    internal static Task<IResult> Check(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");

    /// <summary><c>GET .../payments/{reference}</c> → 200 <see cref="ReceiptDto"/> | 404.</summary>
    internal static Task<IResult> GetReceipt(Guid id, string reference, PaymentService payments, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");
}
