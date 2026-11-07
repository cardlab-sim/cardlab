namespace CardLab.Api.Contracts;

// JSON shapes of design §7 (camelCase on the wire via ASP.NET Core web defaults). Complete in PR-00:
// shared by the endpoint PRs and the integration ApiClient. Amounts are two-decimal strings ("50.00");
// enum values are wire names (see CardLab.Domain.Common.WireNames).

/// <summary><c>GET /api/cards</c> item.</summary>
/// <param name="Id">Card id.</param>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Status"><c>active</c> | <c>frozen</c>.</param>
public sealed record CardSummaryDto(Guid Id, string Name, string CardholderName, string LastFour, string Status);

/// <summary>A limit on the wire.</summary>
/// <param name="Category">Category wire name.</param>
/// <param name="Period"><c>day</c> | <c>week</c> | <c>month</c>.</param>
/// <param name="Amount">Two-decimal string.</param>
public sealed record LimitDto(string Category, string Period, string Amount);

/// <summary><c>GET /api/cards/{id}</c> body.</summary>
/// <param name="Id">Card id.</param>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Status"><c>active</c> | <c>frozen</c>.</param>
/// <param name="Limits">Every limit on the card.</param>
/// <param name="BalanceOwed">002 R-1: balance owed, two-decimal string (set by 002 PR-07; null before it).</param>
public sealed record CardDetailDto(Guid Id, string Name, string CardholderName, string LastFour, string Status, IReadOnlyList<LimitDto> Limits, string? BalanceOwed = null);

/// <summary><c>POST /api/cards/{id}/freeze|unfreeze</c> body.</summary>
/// <param name="Status"><c>active</c> | <c>frozen</c>.</param>
public sealed record StatusDto(string Status);

/// <summary><c>PUT /api/cards/{id}/limits/{category}/{period}</c> request body.</summary>
/// <param name="Amount">Decimal text, e.g. <c>"80.00"</c>.</param>
public sealed record SetLimitRequest(string? Amount);

/// <summary><c>GET /api/cards/{id}/transactions</c> item.</summary>
/// <param name="Id">Transaction id.</param>
/// <param name="OccurredAt">Timestamp as supplied (with its offset).</param>
/// <param name="Amount">Two-decimal string.</param>
/// <param name="Category">Category wire name.</param>
/// <param name="Merchant">Merchant.</param>
/// <param name="Outcome"><c>approved</c> | <c>declined</c>.</param>
/// <param name="Reason">Reason wire name.</param>
public sealed record TransactionDto(Guid Id, DateTimeOffset OccurredAt, string Amount, string Category, string Merchant, string Outcome, string Reason);

/// <summary><c>GET /api/cards/{id}/audit</c> item.</summary>
/// <param name="Id">Audit event id.</param>
/// <param name="AtUtc">When, UTC.</param>
/// <param name="ActorName">Admin display name.</param>
/// <param name="Action">Audit action wire name.</param>
/// <param name="OldValue">Old value text, or null.</param>
/// <param name="NewValue">New value text, or null.</param>
public sealed record AuditDto(Guid Id, DateTimeOffset AtUtc, string ActorName, string Action, string? OldValue, string? NewValue);

/// <summary>A limit's remaining amount in a decision body.</summary>
/// <param name="Category">Category wire name.</param>
/// <param name="Period">Period wire name.</param>
/// <param name="Limit">Two-decimal string.</param>
/// <param name="Remaining">Two-decimal string, never negative.</param>
public sealed record LimitRemainingDto(string Category, string Period, string Limit, string Remaining);

/// <summary>One validation problem in a decision body.</summary>
/// <param name="Field">Wire field name.</param>
/// <param name="Message">Message.</param>
public sealed record ProblemDto(string Field, string Message);

/// <summary>
/// Decision body of <c>POST /api/authorisations</c> and <c>POST /api/cards/{id}/simulate-purchase</c>; same shape for
/// all five reasons (DEC-14). PLANNER-DECISION: <c>problems</c> is always present (empty unless <c>invalid_request</c>).
/// </summary>
/// <param name="Outcome"><c>approved</c> | <c>declined</c>.</param>
/// <param name="Reason">Reason wire name.</param>
/// <param name="TransactionId">Stored transaction id; null when nothing was stored.</param>
/// <param name="Limits">Every limit in the request's category with its remaining amount.</param>
/// <param name="Problems">Validation problems.</param>
public sealed record DecisionDto(string Outcome, string Reason, Guid? TransactionId, IReadOnlyList<LimitRemainingDto> Limits, IReadOnlyList<ProblemDto> Problems);

/// <summary><c>GET /api/health</c> body.</summary>
/// <param name="Commit">Build commit.</param>
/// <param name="Database"><c>ok</c> | <c>unreachable</c>.</param>
public sealed record HealthDto(string Commit, string Database);
