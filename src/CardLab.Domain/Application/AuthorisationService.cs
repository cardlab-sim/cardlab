using System.Diagnostics;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace CardLab.Domain.Application;

/// <summary>
/// Decides and records authorisations for both entry paths (processor by card number, admin simulate by
/// card id), so the decision, the stored transaction and the log event are identical (DEC-14, design §8).
/// </summary>
/// <remarks>
/// Flow: validate (no store access when invalid) → resolve card → inside <c>WithCardLockAsync</c>: read
/// state, for each limit in the category compute <c>PeriodCalendar.RangeContaining(occurredAt, period)</c>
/// and <c>SumApprovedSpendAsync</c>, <c>AuthorisationDecider.Decide</c>, <c>AppendTransaction</c> → log one
/// <see cref="CardLabEvents.AuthorisationDecided"/>. Invalid requests and unknown cards store nothing but
/// are still logged once. The period comes from the request timestamp, never the clock (R-13, AC-33);
/// the clock gives only <c>DecidedAtUtc</c> and the simulate default timestamp.
/// Implemented in PR-08. Unknown-card events carry "-" for card id and last four. Invalid requests log the category
/// and amount only when that field is itself valid (wire name / <c>Money.ToString()</c>), otherwise "-"; raw
/// request text is never logged because it could contain a card number.
/// </remarks>
public sealed class AuthorisationService
{
    private readonly ICardStore _store;
    private readonly IClock _clock;
    private readonly ICardNumberLookup _lookup;
    private readonly ILogger<AuthorisationService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="store">Card store.</param>
    /// <param name="clock">Clock (decision time; simulate default timestamp).</param>
    /// <param name="lookup">Card number lookup (processor path).</param>
    /// <param name="logger">Logger for <see cref="CardLabEvents"/>.</param>
    public AuthorisationService(ICardStore store, IClock clock, ICardNumberLookup lookup, ILogger<AuthorisationService> logger)
    {
        _store = store;
        _clock = clock;
        _lookup = lookup;
        _logger = logger;
    }

    /// <summary>Authorises a processor request identified by card number (<c>POST /api/authorisations</c>).</summary>
    /// <param name="request">Raw request.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>Invalid → declined <c>InvalidRequest</c> with problems, nothing stored; unknown number →
    /// declined <c>CardNotFound</c>, nothing stored; else the decision and the stored transaction id.</returns>
    /// <exception cref="CardLockTimeoutException">Lock not granted in 5 s; nothing stored.</exception>
    public async Task<AuthorisationResult> AuthoriseAsync(AuthorisationRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var validated = TransactionRules.Validate(request);
        if (!validated.IsOk)
        {
            return LogAndReturn(Rejected(validated.Error), null, CategoryForLog(request.Category), AmountForLog(request.Amount), started);
        }

        var hash = _lookup.Compute(validated.Value.CardNumberDigits);
        var cardId = await _store.FindByLookupAsync(hash, ct);
        return cardId is null
            ? LogAndReturn(NotFound(), null, validated.Value.Purchase, started)
            : await DecideAsync(cardId.Value, validated.Value.Purchase, started, ct);
    }

    /// <summary>Authorises a simulated purchase on a card identified by id (<c>POST /api/cards/{id}/simulate-purchase</c>).</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="purchase">Raw purchase; a missing timestamp defaults to <c>IClock.UtcNow</c>.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>Same contract as <see cref="AuthoriseAsync"/>; unknown id → <c>CardNotFound</c>, nothing stored.</returns>
    /// <exception cref="CardLockTimeoutException">Lock not granted in 5 s; nothing stored.</exception>
    public async Task<AuthorisationResult> AuthoriseByCardIdAsync(CardId cardId, SimulatedPurchase purchase, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var validated = TransactionRules.ValidatePurchase(purchase, _clock.UtcNow);
        return validated.IsOk
            ? await DecideAsync(cardId, validated.Value, started, ct)
            : LogAndReturn(Rejected(validated.Error), null, CategoryForLog(purchase.Category), AmountForLog(purchase.Amount), started);
    }

    private static AuthorisationResult Rejected(IReadOnlyList<ValidationProblem> problems) =>
        new(new Decision(Outcome.Declined, Reason.InvalidRequest, []), null, problems);

    private static AuthorisationResult NotFound() =>
        new(new Decision(Outcome.Declined, Reason.CardNotFound, []), null, []);

    private async Task<AuthorisationResult> DecideAsync(CardId cardId, ValidatedPurchase purchase, long started, CancellationToken ct)
    {
        var (result, card) = await _store.WithCardLockAsync(
            cardId,
            async writer =>
            {
                var state = await writer.GetStateAsync(ct);
                if (state is null)
                {
                    return (NotFound(), (CardRef?)null);
                }

                var limits = new List<LimitWithSpend>();
                foreach (var limit in state.Limits.Where(l => l.Category == purchase.Category))
                {
                    var range = PeriodCalendar.RangeContaining(purchase.OccurredAt, limit.Period);
                    limits.Add(new LimitWithSpend(limit, await writer.SumApprovedSpendAsync(purchase.Category, range, ct)));
                }

                var decision = AuthorisationDecider.Decide(state.Status, limits, purchase.Amount);
                var id = writer.AppendTransaction(new NewTransaction(
                    purchase.Amount, purchase.Category, purchase.Merchant, purchase.OccurredAt, _clock.UtcNow, decision.Outcome, decision.Reason));
                return (new AuthorisationResult(decision, id, []), state.Card);
            },
            ct);

        return LogAndReturn(result, card, purchase, started);
    }

    private static string CategoryForLog(string? raw) => WireNames.TryParseCategory(raw, out var category) ? WireNames.ToWire(category) : "-";

    private static string AmountForLog(string? raw) =>
        WireNames.TryParseAmount(raw, out var value) && Money.TryFromDecimal(value, out var money) && MoneyBounds.IsValidTransactionAmount(money)
            ? money.ToString()
            : "-";

    private AuthorisationResult LogAndReturn(AuthorisationResult result, CardRef? card, ValidatedPurchase purchase, long started) =>
        LogAndReturn(result, card, WireNames.ToWire(purchase.Category), purchase.Amount.ToString(), started);

    private AuthorisationResult LogAndReturn(AuthorisationResult result, CardRef? card, string category, string amount, long started)
    {
        CardLabEvents.AuthorisationDecided(
            _logger,
            card is null ? "-" : card.Id.Value.ToString(),
            card?.LastFour ?? "-",
            category,
            amount,
            WireNames.ToWire(result.Decision.Outcome),
            WireNames.ToWire(result.Decision.Reason),
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }
}
