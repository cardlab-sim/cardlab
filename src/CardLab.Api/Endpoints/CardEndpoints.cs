using CardLab.Api.Contracts;
using CardLab.Domain.Application;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
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
    internal static async Task<IResult> ListCards(ICardStore store, CancellationToken ct)
    {
        var cards = await store.ListCardsAsync(ct);
        return TypedResults.Ok(cards.Select(c => new CardSummaryDto(c.Id.Value, c.Name, c.CardholderName, c.LastFour, WireNames.ToWire(c.Status))).ToList());
    }

    /// <summary><c>GET /api/cards/{id}</c> → 200 <see cref="CardDetailDto"/> | 404.</summary>
    internal static async Task<IResult> GetCard(Guid id, ICardStore store, CancellationToken ct)
    {
        var card = await store.GetCardAsync(new CardId(id), ct);
        if (card is null)
        {
            return CardNotFound();
        }

        var limits = card.Limits.Select(ToDto).ToList();
        return TypedResults.Ok(new CardDetailDto(card.Id.Value, card.Name, card.CardholderName, card.LastFour, WireNames.ToWire(card.Status), limits));
    }

    /// <summary><c>POST /api/cards/{id}/freeze</c> → 200 <see cref="StatusDto"/> (<c>frozen</c>, also for a no-op) | 404.</summary>
    internal static async Task<IResult> Freeze(Guid id, CardControlsService controls, CancellationToken ct) =>
        ToStatusResult(await controls.FreezeAsync(new CardId(id), ct));

    /// <summary><c>POST /api/cards/{id}/unfreeze</c> → 200 <see cref="StatusDto"/> (<c>active</c>, also for a no-op) | 404.</summary>
    internal static async Task<IResult> Unfreeze(Guid id, CardControlsService controls, CancellationToken ct) =>
        ToStatusResult(await controls.UnfreezeAsync(new CardId(id), ct));

    /// <summary><c>PUT /api/cards/{id}/limits/{category}/{period}</c> body <see cref="SetLimitRequest"/> → 200 <see cref="LimitDto"/> | 400 | 404.</summary>
    internal static async Task<IResult> SetLimit(Guid id, string category, string period, SetLimitRequest body, CardControlsService controls, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var categoryOk = WireNames.TryParseCategory(category, out var parsedCategory);
        var periodOk = WireNames.TryParsePeriod(period, out var parsedPeriod);
        var amountOk = WireNames.TryParseAmount(body?.Amount, out var amount);
        if (!categoryOk)
        {
            errors["category"] = ["Unknown category."];
        }

        if (!periodOk)
        {
            errors["period"] = ["Unknown period. Use day, week or month."];
        }

        if (!amountOk)
        {
            errors["amount"] = ["Amount must be a number such as 50.00."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await controls.SetLimitAsync(new CardId(id), parsedCategory, parsedPeriod, amount, ct);
        if (result.IsOk)
        {
            return TypedResults.Ok(ToDto(result.Value));
        }

        return result.Error == DomainError.InvalidAmount
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Amount must be between 0.00 and 1000000.00 with at most two decimal places."] })
            : CardNotFound();
    }

    /// <summary><c>DELETE /api/cards/{id}/limits/{category}/{period}</c> → 204 (also for a no-op) | 400 (unknown category/period) | 404.</summary>
    internal static async Task<IResult> RemoveLimit(Guid id, string category, string period, CardControlsService controls, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!WireNames.TryParseCategory(category, out var parsedCategory))
        {
            errors["category"] = ["Unknown category."];
        }

        if (!WireNames.TryParsePeriod(period, out var parsedPeriod))
        {
            errors["period"] = ["Unknown period. Use day, week or month."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await controls.RemoveLimitAsync(new CardId(id), parsedCategory, parsedPeriod, ct);
        return result.IsOk ? TypedResults.NoContent() : CardNotFound();
    }

    /// <summary><c>GET /api/cards/{id}/transactions</c> → 200 <see cref="TransactionDto"/>[] newest first | 404.</summary>
    internal static async Task<IResult> ListTransactions(Guid id, ICardStore store, CancellationToken ct)
    {
        var cardId = new CardId(id);
        if (await store.GetCardAsync(cardId, ct) is null)
        {
            return CardNotFound();
        }

        var transactions = await store.ListTransactionsAsync(cardId, ct);
        return TypedResults.Ok(transactions.Select(t => new TransactionDto(
            t.Id.Value,
            t.OccurredAt,
            t.Amount.ToString(),
            WireNames.ToWire(t.Category),
            t.Merchant,
            WireNames.ToWire(t.Outcome),
            WireNames.ToWire(t.Reason))).ToList());
    }

    /// <summary><c>GET /api/cards/{id}/audit</c> → 200 <see cref="AuditDto"/>[] newest first | 404.</summary>
    internal static async Task<IResult> ListAudit(Guid id, ICardStore store, CancellationToken ct)
    {
        var cardId = new CardId(id);
        if (await store.GetCardAsync(cardId, ct) is null)
        {
            return CardNotFound();
        }

        var events = await store.ListAuditAsync(cardId, ct);
        return TypedResults.Ok(events.Select(a => new AuditDto(
            a.Id.Value,
            a.Event.AtUtc,
            a.Event.Actor.Name,
            WireNames.ToWire(a.Event.Action),
            a.Event.OldValue,
            a.Event.NewValue)).ToList());
    }

    private static IResult ToStatusResult(Result<CardStatus, DomainError> result) =>
        result.IsOk ? TypedResults.Ok(new StatusDto(WireNames.ToWire(result.Value))) : CardNotFound();

    private static IResult CardNotFound() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Card not found");

    private static LimitDto ToDto(SpendLimit limit) => new(WireNames.ToWire(limit.Category), WireNames.ToWire(limit.Period), limit.Amount.ToString());
}
