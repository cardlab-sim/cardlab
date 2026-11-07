using CardLab.Api.Contracts;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.FakeBank;
using CardLab.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CardLab.Api.Endpoints;

/// <summary>
/// The fake bank's labelled steering control and ledger view under <c>/api/fake-bank</c> (002 R-15, DEC-6, DEC-7).
/// Mapped in every environment, including <c>demo</c>.
/// </summary>
/// <remarks>
/// Wire values (PR-06): next debit <c>complete</c> | <c>refuse_insufficient_funds</c> | <c>refuse_account_unavailable</c> |
/// <c>timeout_debit_applied</c> | <c>timeout_not_received</c> (unset reads as <c>complete</c>); next status check
/// <c>answers</c> | <c>does_not_answer</c> (unset reads as <c>answers</c>). PUT merges: absent fields keep their value;
/// an unknown value → 400 ValidationProblem, nothing stored. The account is the seeded
/// <c>CardLab:Seed:LinkedAccount:BankAccountRef</c>. Route table complete in PR-00; handlers implemented in PR-06.
/// </remarks>
public static class FakeBankEndpoints
{
    /// <summary>Maps the fake-bank routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapFakeBankEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fake-bank", Get);
        app.MapPut("/api/fake-bank/steering", SetSteering);
        return app;
    }

    /// <summary><c>GET /api/fake-bank</c> → 200 <see cref="FakeBankDto"/>.</summary>
    internal static Task<IResult> Get(IFakeBankSteering steering, ICurrentAccountBank bank, IOptions<SeedOptions> seed, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");

    /// <summary><c>PUT /api/fake-bank/steering</c> body <see cref="SteeringRequest"/> → 200 <see cref="FakeBankDto"/> | 400.</summary>
    internal static Task<IResult> SetSteering(SteeringRequest body, IFakeBankSteering steering, ICurrentAccountBank bank, IOptions<SeedOptions> seed, CancellationToken ct) =>
        throw new NotImplementedException("PR-06");
}
