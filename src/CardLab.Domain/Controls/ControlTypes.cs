using CardLab.Domain.Common;

namespace CardLab.Domain.Controls;

/// <summary>Whether a card can transact (R-1, R-2).</summary>
public enum CardStatus
{
    /// <summary>Transactions are decided against limits.</summary>
    Active,

    /// <summary>Every transaction declines with <c>card_frozen</c> (R-3).</summary>
    Frozen,
}

/// <summary>Kinds of control change recorded as audit events (R-6).</summary>
public enum AuditAction
{
    /// <summary>Active → Frozen.</summary>
    Freeze,

    /// <summary>Frozen → Active.</summary>
    Unfreeze,

    /// <summary>A limit was created for a (category, period) that had none.</summary>
    LimitSet,

    /// <summary>An existing limit's amount was replaced.</summary>
    LimitChanged,

    /// <summary>A limit was removed (the category becomes unlimited for that period).</summary>
    LimitRemoved,
}

/// <summary>Who made a control change. In this feature always the seeded admin (no login).</summary>
/// <param name="Id">Admin user id.</param>
/// <param name="Name">Display name, e.g. "Sam Ellis".</param>
public sealed record Actor(Guid Id, string Name);

/// <summary>A card's identity as audit events and log events show it (internal id + last four, never the number).</summary>
/// <param name="Id">Internal card id.</param>
/// <param name="LastFour">Last four digits of the card number.</param>
public sealed record CardRef(CardId Id, string LastFour);

/// <summary>An immutable record of one control change (R-6). Append-only; never edited or deleted.</summary>
/// <remarks>PLANNER-DECISION: carries <see cref="CardRef"/> (id + last four, as R-6/AC-1 require) instead of
/// a bare CardId; the store persists only the id and joins the card for the last four.</remarks>
/// <param name="Card">The card changed.</param>
/// <param name="Actor">Who changed it.</param>
/// <param name="AtUtc">When, UTC (from <c>IClock</c>).</param>
/// <param name="Action">What kind of change.</param>
/// <param name="OldValue">Previous value: <c>"Active"</c>/<c>"Frozen"</c> for status, <c>"Fuel/Day/50.00"</c>
/// for limits (category/period enum names + <c>Money.ToString()</c>); null for <see cref="AuditAction.LimitSet"/>.</param>
/// <param name="NewValue">New value in the same format; null for <see cref="AuditAction.LimitRemoved"/>.</param>
public sealed record AuditEvent(CardRef Card, Actor Actor, DateTimeOffset AtUtc, AuditAction Action, string? OldValue, string? NewValue);

/// <summary>The new state produced by a control change plus the audit event that records it.</summary>
/// <typeparam name="T">State type (<see cref="CardStatus"/> or <c>SpendLimit</c>).</typeparam>
/// <param name="NewState">State after the change.</param>
/// <param name="Event">The audit event to append in the same unit of work.</param>
public sealed record ControlChange<T>(T NewState, AuditEvent Event);
