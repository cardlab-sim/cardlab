using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace CardLab.Domain.Application;

/// <summary>
/// Admin control operations (R-1, R-2, R-4, R-6): each runs inside <c>ICardStore.WithCardLockAsync</c>,
/// applies <see cref="CardControls"/>, persists the new state and its audit event in the same unit of
/// work, and logs one <see cref="CardLabEvents.ControlChanged"/> per real change (none for no-ops).
/// </summary>
/// <remarks>Actor from <see cref="ICurrentActor"/>, audit time from <see cref="IClock"/>.</remarks>
public sealed class CardControlsService
{
    private readonly ICardStore _store;
    private readonly IClock _clock;
    private readonly ICurrentActor _actor;
    private readonly ILogger<CardControlsService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="store">Card store.</param>
    /// <param name="clock">Clock for audit times.</param>
    /// <param name="actor">Acting admin.</param>
    /// <param name="logger">Logger for <see cref="CardLabEvents"/>.</param>
    public CardControlsService(ICardStore store, IClock clock, ICurrentActor actor, ILogger<CardControlsService> logger)
    {
        _store = store;
        _clock = clock;
        _actor = actor;
        _logger = logger;
    }

    /// <summary>Freezes a card; no-op (no audit, no log) when already frozen.</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The status after the call (Frozen), or <see cref="DomainError.CardNotFound"/>.</returns>
    public Task<Result<CardStatus, DomainError>> FreezeAsync(CardId cardId, CancellationToken ct = default) =>
        ChangeStatusAsync(cardId, CardControls.Freeze, CardStatus.Frozen, ct);

    /// <summary>Unfreezes a card; no-op (no audit, no log) when already active.</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The status after the call (Active), or <see cref="DomainError.CardNotFound"/>.</returns>
    public Task<Result<CardStatus, DomainError>> UnfreezeAsync(CardId cardId, CancellationToken ct = default) =>
        ChangeStatusAsync(cardId, CardControls.Unfreeze, CardStatus.Active, ct);

    /// <summary>Sets or replaces a limit (audit <c>LimitSet</c> or <c>LimitChanged</c>).</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="amount">Requested amount in pounds; must pass <c>Money.TryFromDecimal</c> and
    /// <c>MoneyBounds.IsValidLimitAmount</c>, checked before the store is touched.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The stored limit; <see cref="DomainError.InvalidAmount"/> (nothing stored, no audit, AC-9) or
    /// <see cref="DomainError.CardNotFound"/>.</returns>
    public async Task<Result<SpendLimit, DomainError>> SetLimitAsync(CardId cardId, Category category, Period period, decimal amount, CancellationToken ct = default)
    {
        if (!Money.TryFromDecimal(amount, out var money) || !MoneyBounds.IsValidLimitAmount(money))
        {
            return Result<SpendLimit, DomainError>.Fail(DomainError.InvalidAmount);
        }

        var requested = new SpendLimit(category, period, money);
        var outcome = await _store.WithCardLockAsync(
            cardId,
            async w =>
            {
                var state = await w.GetStateAsync(ct);
                if (state is null)
                {
                    return Result<AuditEvent, DomainError>.Fail(DomainError.CardNotFound);
                }

                var existing = state.Limits.FirstOrDefault(l => l.Category == category && l.Period == period);
                var change = CardControls.SetLimit(existing, requested, state.Card, _actor.Current, _clock.UtcNow);
                w.UpsertLimit(change.NewState);
                w.AppendAudit(change.Event);
                return Result<AuditEvent, DomainError>.Ok(change.Event);
            },
            ct);

        if (!outcome.IsOk)
        {
            return Result<SpendLimit, DomainError>.Fail(outcome.Error);
        }

        LogChange(outcome.Value);
        return Result<SpendLimit, DomainError>.Ok(requested);
    }

    /// <summary>Removes a limit (audit <c>LimitRemoved</c>); no-op when there is none.</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when a limit was removed, false for a no-op; or <see cref="DomainError.CardNotFound"/>.</returns>
    public async Task<Result<bool, DomainError>> RemoveLimitAsync(CardId cardId, Category category, Period period, CancellationToken ct = default)
    {
        var outcome = await _store.WithCardLockAsync(
            cardId,
            async w =>
            {
                var state = await w.GetStateAsync(ct);
                if (state is null)
                {
                    return Result<AuditEvent?, DomainError>.Fail(DomainError.CardNotFound);
                }

                var existing = state.Limits.FirstOrDefault(l => l.Category == category && l.Period == period);
                var change = CardControls.RemoveLimit(existing, state.Card, _actor.Current, _clock.UtcNow);
                if (change is null)
                {
                    return Result<AuditEvent?, DomainError>.Ok(null);
                }

                w.RemoveLimit(category, period);
                w.AppendAudit(change.Event);
                return Result<AuditEvent?, DomainError>.Ok(change.Event);
            },
            ct);

        if (!outcome.IsOk)
        {
            return Result<bool, DomainError>.Fail(outcome.Error);
        }

        if (outcome.Value is null)
        {
            return Result<bool, DomainError>.Ok(false);
        }

        LogChange(outcome.Value);
        return Result<bool, DomainError>.Ok(true);
    }

    private async Task<Result<CardStatus, DomainError>> ChangeStatusAsync(
        CardId cardId,
        Func<CardStatus, CardRef, Actor, DateTimeOffset, ControlChange<CardStatus>?> rule,
        CardStatus target,
        CancellationToken ct)
    {
        var outcome = await _store.WithCardLockAsync(
            cardId,
            async w =>
            {
                var state = await w.GetStateAsync(ct);
                if (state is null)
                {
                    return Result<AuditEvent?, DomainError>.Fail(DomainError.CardNotFound);
                }

                var change = rule(state.Status, state.Card, _actor.Current, _clock.UtcNow);
                if (change is null)
                {
                    return Result<AuditEvent?, DomainError>.Ok(null);
                }

                w.SetStatus(change.NewState);
                w.AppendAudit(change.Event);
                return Result<AuditEvent?, DomainError>.Ok(change.Event);
            },
            ct);

        if (!outcome.IsOk)
        {
            return Result<CardStatus, DomainError>.Fail(outcome.Error);
        }

        if (outcome.Value is not null)
        {
            LogChange(outcome.Value);
        }

        return Result<CardStatus, DomainError>.Ok(target);
    }

    private void LogChange(AuditEvent change) =>
        CardLabEvents.ControlChanged(
            _logger,
            change.Actor.Id.ToString(),
            change.Actor.Name,
            change.Card.Id.Value.ToString(),
            change.Card.LastFour,
            WireNames.ToWire(change.Action));
}
