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
/// <remarks>Actor from <see cref="ICurrentActor"/>, audit time from <see cref="IClock"/>. Implemented in PR-05.</remarks>
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
    public Task<Result<CardStatus, DomainError>> FreezeAsync(CardId cardId, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <summary>Unfreezes a card; no-op (no audit, no log) when already active.</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The status after the call (Active), or <see cref="DomainError.CardNotFound"/>.</returns>
    public Task<Result<CardStatus, DomainError>> UnfreezeAsync(CardId cardId, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <summary>Sets or replaces a limit (audit <c>LimitSet</c> or <c>LimitChanged</c>).</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="amount">Requested amount in pounds; must pass <c>Money.TryFromDecimal</c> and
    /// <c>MoneyBounds.IsValidLimitAmount</c>, checked before the store is touched.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The stored limit; <see cref="DomainError.InvalidAmount"/> (nothing stored, no audit, AC-9) or
    /// <see cref="DomainError.CardNotFound"/>.</returns>
    public Task<Result<SpendLimit, DomainError>> SetLimitAsync(CardId cardId, Category category, Period period, decimal amount, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <summary>Removes a limit (audit <c>LimitRemoved</c>); no-op when there is none.</summary>
    /// <param name="cardId">Card id.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when a limit was removed, false for a no-op; or <see cref="DomainError.CardNotFound"/>.</returns>
    public Task<Result<bool, DomainError>> RemoveLimitAsync(CardId cardId, Category category, Period period, CancellationToken ct = default) => throw new NotImplementedException("PR-05");
}
