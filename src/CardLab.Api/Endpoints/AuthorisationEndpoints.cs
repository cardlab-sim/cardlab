using CardLab.Api.Contracts;
using CardLab.Domain.Application;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;

namespace CardLab.Api.Endpoints;

/// <summary>
/// The processor API and the admin simulate endpoint (DEC-14). Both call <see cref="AuthorisationService"/> and
/// return a <see cref="DecisionDto"/>: 200 for <c>approved</c>, <c>card_frozen</c>, <c>limit_exceeded</c>; 404 for
/// <c>card_not_found</c>; 400 for <c>invalid_request</c> (with <c>problems</c>). The card number is never echoed.
/// </summary>
/// <remarks>Route table complete in PR-00; handler bodies implemented in PR-08.</remarks>
public static class AuthorisationEndpoints
{
    /// <summary>Maps <c>POST /api/authorisations</c> and <c>POST /api/cards/{id}/simulate-purchase</c>.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapAuthorisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/authorisations", Authorise);
        app.MapPost("/api/cards/{id:guid}/simulate-purchase", SimulatePurchase);
        return app;
    }

    /// <summary><c>POST /api/authorisations</c> body <see cref="AuthorisationRequest"/> → <see cref="DecisionDto"/> with 200/400/404.</summary>
    internal static async Task<IResult> Authorise(AuthorisationRequest body, AuthorisationService service, CancellationToken ct) =>
        ToResponse(await service.AuthoriseAsync(body, ct));

    /// <summary><c>POST /api/cards/{id}/simulate-purchase</c> body <see cref="SimulatedPurchase"/> → <see cref="DecisionDto"/> with 200/400/404.</summary>
    internal static async Task<IResult> SimulatePurchase(Guid id, SimulatedPurchase body, AuthorisationService service, CancellationToken ct) =>
        ToResponse(await service.AuthoriseByCardIdAsync(new CardId(id), body, ct));

    private static IResult ToResponse(AuthorisationResult result)
    {
        var decision = result.Decision;
        var dto = new DecisionDto(
            WireNames.ToWire(decision.Outcome),
            WireNames.ToWire(decision.Reason),
            result.TransactionId?.Value,
            decision.Limits
                .Select(l => new LimitRemainingDto(WireNames.ToWire(l.Category), WireNames.ToWire(l.Period), l.Limit.ToString(), l.Remaining.ToString()))
                .ToList(),
            result.Problems.Select(p => new ProblemDto(p.Field, p.Message)).ToList());
        var status = decision.Reason switch
        {
            Reason.CardNotFound => StatusCodes.Status404NotFound,
            Reason.InvalidRequest => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status200OK,
        };
        return TypedResults.Json(dto, statusCode: status);
    }
}
