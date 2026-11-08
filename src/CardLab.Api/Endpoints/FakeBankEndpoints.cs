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
    internal static async Task<IResult> Get(IFakeBankSteering steering, ICurrentAccountBank bank, IOptions<SeedOptions> seed, CancellationToken ct) =>
        TypedResults.Ok(await ReadAsync(steering, bank, seed, ct));

    /// <summary><c>PUT /api/fake-bank/steering</c> body <see cref="SteeringRequest"/> → 200 <see cref="FakeBankDto"/> | 400.</summary>
    internal static async Task<IResult> SetSteering(SteeringRequest body, IFakeBankSteering steering, ICurrentAccountBank bank, IOptions<SeedOptions> seed, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        NextDebitOutcome? debit = null;
        NextStatusCheck? status = null;
        if (body.NextDebitOutcome is not null)
        {
            if (DebitOutcomes.TryGetValue(body.NextDebitOutcome, out var parsed))
            {
                debit = parsed;
            }
            else
            {
                errors["nextDebitOutcome"] = ["Unknown next debit outcome."];
            }
        }

        if (body.NextStatusCheck is not null)
        {
            if (StatusChecks.TryGetValue(body.NextStatusCheck, out var parsed))
            {
                status = parsed;
            }
            else
            {
                errors["nextStatusCheck"] = ["Unknown next status check."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var current = await steering.GetAsync(ct);
        await steering.SetAsync(new Steering(debit ?? current.NextDebit, status ?? current.NextStatusCheck), ct);
        return TypedResults.Ok(await ReadAsync(steering, bank, seed, ct));
    }

    private static readonly Dictionary<string, NextDebitOutcome> DebitOutcomes = new()
    {
        ["complete"] = NextDebitOutcome.Complete,
        ["refuse_insufficient_funds"] = NextDebitOutcome.RefuseInsufficientFunds,
        ["refuse_account_unavailable"] = NextDebitOutcome.RefuseAccountUnavailable,
        ["timeout_debit_applied"] = NextDebitOutcome.TimeoutDebitApplied,
        ["timeout_not_received"] = NextDebitOutcome.TimeoutNotReceived,
    };

    private static readonly Dictionary<string, NextStatusCheck> StatusChecks = new()
    {
        ["answers"] = NextStatusCheck.Answers,
        ["does_not_answer"] = NextStatusCheck.DoesNotAnswer,
    };

    private static async Task<FakeBankDto> ReadAsync(IFakeBankSteering steering, ICurrentAccountBank bank, IOptions<SeedOptions> seed, CancellationToken ct)
    {
        var current = await steering.GetAsync(ct);
        var account = new BankAccountRef(seed.Value.LinkedAccount.BankAccountRef);
        var snapshot = await bank.GetAccountAsync(account, ct);
        var entries = await bank.ListRecentEntriesAsync(account, 10, ct);
        return new FakeBankDto(
            DebitOutcomes.First(p => p.Value == (current.NextDebit ?? NextDebitOutcome.Complete)).Key,
            StatusChecks.First(p => p.Value == (current.NextStatusCheck ?? NextStatusCheck.Answers)).Key,
            new FakeBankAccountDto(snapshot.LastFour, snapshot.AvailableBalance.ToString()),
            entries.Select(PaymentDtoMapper.ToDto).ToList());
    }
}
