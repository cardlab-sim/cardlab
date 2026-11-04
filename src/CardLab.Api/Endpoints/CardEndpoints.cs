using CardLab.Api.Contracts;
using CardLab.Domain.Application;
using CardLab.Domain.Ports;

namespace CardLab.Api.Endpoints;

/// <summary>
/// Admin card endpoints under <c>/api/cards</c> (design §7, DEC-19). Thin: parse route/body → service or store →
/// map to DTO / status. Errors are ProblemDetails: unknown card → 404; amount outside R-11, bad amount text, unknown
/// category or period → 400 (with <c>errors</c>), nothing stored (AC-9).
/// </summary>
/// <remarks>Route table complete in PR-00 so the host starts; handler bodies implemented in PR-09.</remarks>
public static class CardEndpoints
{
    /// <summary>Maps the card routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var cards = app.MapGroup("/api/cards");
        cards.MapGet("/", ListCards);
        cards.MapGet("/{id:guid}", GetCard);
        cards.MapPost("/{id:guid}/freeze", Freeze);
        cards.MapPost("/{id:guid}/unfreeze", Unfreeze);
        cards.MapPut("/{id:guid}/limits/{category}/{period}", SetLimit);
        cards.MapDelete("/{id:guid}/limits/{category}/{period}", RemoveLimit);
        cards.MapGet("/{id:guid}/transactions", ListTransactions);
        cards.MapGet("/{id:guid}/audit", ListAudit);
        return app;
    }

    /// <summary><c>GET /api/cards</c> → 200 <see cref="CardSummaryDto"/>[].</summary>
    internal static Task<IResult> ListCards(ICardStore store, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>GET /api/cards/{id}</c> → 200 <see cref="CardDetailDto"/> | 404.</summary>
    internal static Task<IResult> GetCard(Guid id, ICardStore store, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>POST /api/cards/{id}/freeze</c> → 200 <see cref="StatusDto"/> (<c>frozen</c>, also for a no-op) | 404.</summary>
    internal static Task<IResult> Freeze(Guid id, CardControlsService controls, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>POST /api/cards/{id}/unfreeze</c> → 200 <see cref="StatusDto"/> (<c>active</c>, also for a no-op) | 404.</summary>
    internal static Task<IResult> Unfreeze(Guid id, CardControlsService controls, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>PUT /api/cards/{id}/limits/{category}/{period}</c> body <see cref="SetLimitRequest"/> → 200 <see cref="LimitDto"/> | 400 | 404.</summary>
    internal static Task<IResult> SetLimit(Guid id, string category, string period, SetLimitRequest body, CardControlsService controls, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>DELETE /api/cards/{id}/limits/{category}/{period}</c> → 204 (also for a no-op) | 400 (unknown category/period) | 404.</summary>
    internal static Task<IResult> RemoveLimit(Guid id, string category, string period, CardControlsService controls, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>GET /api/cards/{id}/transactions</c> → 200 <see cref="TransactionDto"/>[] newest first | 404.</summary>
    internal static Task<IResult> ListTransactions(Guid id, ICardStore store, CancellationToken ct) => throw new NotImplementedException("PR-09");

    /// <summary><c>GET /api/cards/{id}/audit</c> → 200 <see cref="AuditDto"/>[] newest first | 404.</summary>
    internal static Task<IResult> ListAudit(Guid id, ICardStore store, CancellationToken ct) => throw new NotImplementedException("PR-09");
}
