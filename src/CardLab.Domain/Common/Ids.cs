namespace CardLab.Domain.Common;

/// <summary>Internal identifier of a card (never the card number; NFR-1).</summary>
/// <param name="Value">Database key of the card.</param>
public readonly record struct CardId(Guid Value);

/// <summary>Identifier of a stored transaction (an authorisation decision for a known card).</summary>
/// <param name="Value">Database key, assigned by the store on append.</param>
public readonly record struct TransactionId(Guid Value);

/// <summary>Identifier of a stored audit event.</summary>
/// <param name="Value">Database key, assigned by the store on append.</param>
public readonly record struct AuditEventId(Guid Value);
