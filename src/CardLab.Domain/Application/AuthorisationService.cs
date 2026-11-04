using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
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
/// Implemented in PR-08.
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
    public Task<AuthorisationResult> AuthoriseAsync(AuthorisationRequest request, CancellationToken ct = default) => throw new NotImplementedException("PR-08");

    /// <summary>Authorises a simulated purchase on a card identified by id (<c>POST /api/cards/{id}/simulate-purchase</c>).</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="purchase">Raw purchase; a missing timestamp defaults to <c>IClock.UtcNow</c>.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>Same contract as <see cref="AuthoriseAsync"/>; unknown id → <c>CardNotFound</c>, nothing stored.</returns>
    /// <exception cref="CardLockTimeoutException">Lock not granted in 5 s; nothing stored.</exception>
    public Task<AuthorisationResult> AuthoriseByCardIdAsync(CardId cardId, SimulatedPurchase purchase, CancellationToken ct = default) => throw new NotImplementedException("PR-08");
}
