using CardLab.Domain.Limits;

namespace CardLab.Domain.Controls;

/// <summary>
/// Pure control-change rules (R-1, R-2, R-4, R-6): compute the new state and its audit event, or
/// <c>null</c> for a no-op (no state change, no audit event).
/// </summary>
/// <remarks>
/// Audit value format: status as enum name (<c>"Active"</c>, <c>"Frozen"</c>); limit as
/// <c>"{Category}/{Period}/{Money}"</c>, e.g. <c>"Fuel/Day/50.00"</c>. Implemented in PR-05.
/// </remarks>
public static class CardControls
{
    /// <summary>Freezes an active card.</summary>
    /// <param name="current">Current status.</param>
    /// <param name="card">Card identity for the audit event.</param>
    /// <param name="actor">Who freezes it.</param>
    /// <param name="nowUtc">Audit time.</param>
    /// <returns>Frozen + a <see cref="AuditAction.Freeze"/> event (old "Active", new "Frozen"); null if already frozen (AC-2).</returns>
    public static ControlChange<CardStatus>? Freeze(CardStatus current, CardRef card, Actor actor, DateTimeOffset nowUtc) => throw new NotImplementedException("PR-05");

    /// <summary>Unfreezes a frozen card.</summary>
    /// <param name="current">Current status.</param>
    /// <param name="card">Card identity for the audit event.</param>
    /// <param name="actor">Who unfreezes it.</param>
    /// <param name="nowUtc">Audit time.</param>
    /// <returns>Active + an <see cref="AuditAction.Unfreeze"/> event (old "Frozen", new "Active"); null if already active (AC-40).</returns>
    public static ControlChange<CardStatus>? Unfreeze(CardStatus current, CardRef card, Actor actor, DateTimeOffset nowUtc) => throw new NotImplementedException("PR-05");

    /// <summary>Sets or replaces the limit for the requested (category, period).</summary>
    /// <param name="existing">The card's current limit for the same (category, period), or null.</param>
    /// <param name="requested">The new limit (amount already validated by the caller with <c>MoneyBounds.IsValidLimitAmount</c>).</param>
    /// <param name="card">Card identity for the audit event.</param>
    /// <param name="actor">Who sets it.</param>
    /// <param name="nowUtc">Audit time.</param>
    /// <returns>The requested limit + <see cref="AuditAction.LimitSet"/> (old null) when none existed, else
    /// <see cref="AuditAction.LimitChanged"/> with old and new values (AC-6, AC-7). Always a change.</returns>
    public static ControlChange<SpendLimit> SetLimit(SpendLimit? existing, SpendLimit requested, CardRef card, Actor actor, DateTimeOffset nowUtc) => throw new NotImplementedException("PR-05");

    /// <summary>Removes a limit.</summary>
    /// <param name="existing">The card's current limit for the (category, period), or null.</param>
    /// <param name="card">Card identity for the audit event.</param>
    /// <param name="actor">Who removes it.</param>
    /// <param name="nowUtc">Audit time.</param>
    /// <returns>The removed limit + a <see cref="AuditAction.LimitRemoved"/> event (old value, new null) (AC-8); null when no such limit.</returns>
    public static ControlChange<SpendLimit>? RemoveLimit(SpendLimit? existing, CardRef card, Actor actor, DateTimeOffset nowUtc) => throw new NotImplementedException("PR-05");
}
