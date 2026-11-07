using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;

namespace CardLab.Domain.Cards;

/// <summary>A card as listed on the card list page (R-8).</summary>
/// <param name="Id">Internal id.</param>
/// <param name="Name">Card name, e.g. "Ops van".</param>
/// <param name="CardholderName">Cardholder, e.g. "Priya Nair".</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Status">Active or frozen.</param>
public sealed record CardSummary(CardId Id, string Name, string CardholderName, string LastFour, CardStatus Status);

/// <summary>A card with its limits (card detail page, <c>GET /api/cards/{id}</c>).</summary>
/// <param name="Id">Internal id.</param>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Status">Active or frozen.</param>
/// <param name="Limits">Every limit, ordered by category then period (enum order).</param>
public sealed record CardDetail(CardId Id, string Name, string CardholderName, string LastFour, CardStatus Status, IReadOnlyList<SpendLimit> Limits);

/// <summary>A card's controllable state as read inside the card lock.</summary>
/// <param name="Card">Identity (id + last four).</param>
/// <param name="Status">Active or frozen.</param>
/// <param name="Limits">Every limit on the card.</param>
public sealed record CardState(CardRef Card, CardStatus Status, IReadOnlyList<SpendLimit> Limits);

/// <summary>A transaction to append (the store assigns the id). Append-only.</summary>
/// <param name="Amount">Validated amount.</param>
/// <param name="Category">Category.</param>
/// <param name="Merchant">Merchant.</param>
/// <param name="OccurredAt">Timestamp as supplied (whole seconds, original offset).</param>
/// <param name="DecidedAtUtc">When the decision was made (<c>IClock.UtcNow</c>).</param>
/// <param name="Outcome">Approved or declined.</param>
/// <param name="Reason">Approved, CardFrozen or LimitExceeded (other reasons are never stored).</param>
public sealed record NewTransaction(Money Amount, Category Category, string Merchant, DateTimeOffset OccurredAt, DateTimeOffset DecidedAtUtc, Outcome Outcome, Reason Reason);

/// <summary>A stored transaction (history row, R-7, R-8).</summary>
/// <param name="Id">Store-assigned id.</param>
/// <param name="CardId">Card.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Category">Category.</param>
/// <param name="Merchant">Merchant.</param>
/// <param name="OccurredAt">Timestamp as supplied.</param>
/// <param name="DecidedAtUtc">Decision time, UTC.</param>
/// <param name="Outcome">Outcome.</param>
/// <param name="Reason">Reason.</param>
public sealed record TransactionRecord(TransactionId Id, CardId CardId, Money Amount, Category Category, string Merchant, DateTimeOffset OccurredAt, DateTimeOffset DecidedAtUtc, Outcome Outcome, Reason Reason);

/// <summary>A stored audit event with its store-assigned id.</summary>
/// <param name="Id">Store-assigned id.</param>
/// <param name="Event">The event (card ref includes the last four).</param>
public sealed record AuditRecord(AuditEventId Id, AuditEvent Event);
