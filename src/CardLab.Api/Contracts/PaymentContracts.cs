namespace CardLab.Api.Contracts;

// JSON shapes of 002 design §7 (camelCase on the wire). Complete in PR-00: shared by PR-06 (cardholder + fake bank),
// PR-07 (admin) and the integration ApiClient. Amounts are two-decimal strings ("150.50"); times ISO-8601 with offset;
// enum values are wire names (PaymentWireNames). Never a card or account number beyond the last four (NFR-3).

/// <summary>Card part of the cardholder view.</summary>
/// <param name="Id">Card id.</param>
/// <param name="Name">Card name.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="CardholderName">The acting person (R-16).</param>
/// <param name="Status"><c>active</c> | <c>frozen</c>.</param>
public sealed record CardholderCardDto(Guid Id, string Name, string LastFour, string CardholderName, string Status);

/// <summary>Linked account part of the cardholder view.</summary>
/// <param name="DisplayName">Account name.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="AvailableBalance">Available balance (from the bank).</param>
/// <param name="AuthorityBasis">Standing authority text.</param>
public sealed record LinkedAccountDto(string DisplayName, string LastFour, string AvailableBalance, string AuthorityBasis);

/// <summary>A card activity row (cardholder view and <c>GET /api/cards/{id}/activity</c>).</summary>
/// <param name="At">Purchase decision time / payment acceptance time.</param>
/// <param name="Kind"><c>purchase</c> | <c>payment</c>.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Description">Merchant, or "Payment PAY-…".</param>
/// <param name="Outcome">Purchases: <c>approved</c> | <c>declined</c>; payments: null.</param>
/// <param name="State">Payments: <c>pending</c> | <c>completed</c> | <c>failed</c>; purchases: null.</param>
/// <param name="Reference">Payments: the reference; purchases: null.</param>
/// <param name="Origin">Payments: <c>cardholder_view</c> | <c>deploy_smoke</c>; purchases: null.</param>
/// <param name="Category">Purchases: category wire name; payments: null (PLANNER-DECISION, keeps 001's admin columns).</param>
/// <param name="Reason">Purchases: reason wire name; payments: null.</param>
public sealed record ActivityDto(DateTimeOffset At, string Kind, string Amount, string Description, string? Outcome, string? State, string? Reference, string? Origin, string? Category, string? Reason);

/// <summary>A current-account ledger entry.</summary>
/// <param name="Reference">Payment reference.</param>
/// <param name="Amount">Debited amount.</param>
/// <param name="PostedAt">Posting time.</param>
/// <param name="Narrative">Bank narrative.</param>
public sealed record AccountEntryDto(string Reference, string Amount, DateTimeOffset PostedAt, string Narrative);

/// <summary>Receipt body (confirm, check, receipt reload; also <c>pendingPayment</c> in the view).</summary>
/// <param name="Reference">Payment reference.</param>
/// <param name="State"><c>pending</c> | <c>completed</c> | <c>failed</c>.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Fee">Fee ("0.00").</param>
/// <param name="Time">Effective time when settled; acceptance time when pending.</param>
/// <param name="From">Source label.</param>
/// <param name="To">Destination label.</param>
/// <param name="FailureReason">Failure reason wire name, or null.</param>
/// <param name="Message">W-2..W-5 / W-10 verbatim.</param>
/// <param name="BalanceOwed">Balance owed now.</param>
/// <param name="Repeated">True for a repeated confirm.</param>
/// <param name="Origin">Origin wire name.</param>
public sealed record ReceiptDto(string Reference, string State, string Amount, string Fee, DateTimeOffset Time, string From, string To, string? FailureReason, string Message, string BalanceOwed, bool Repeated, string Origin);

/// <summary><c>GET /api/cardholder/cards/{id}</c> body.</summary>
/// <param name="Card">Card.</param>
/// <param name="BalanceOwed">Balance owed.</param>
/// <param name="Availability"><c>available</c> | <c>nothing_to_pay</c> | <c>above_limit</c> | <c>payment_pending</c>.</param>
/// <param name="AvailabilityMessage">W-7 / W-8 / W-9, or null.</param>
/// <param name="PendingPayment">Pending payment receipt (W-5), or null.</param>
/// <param name="Account">Linked account.</param>
/// <param name="CardActivity">Last 10 card rows, newest first.</param>
/// <param name="AccountActivity">Last 10 ledger entries, newest first.</param>
public sealed record CardholderViewDto(
    CardholderCardDto Card,
    string BalanceOwed,
    string Availability,
    string? AvailabilityMessage,
    ReceiptDto? PendingPayment,
    LinkedAccountDto Account,
    IReadOnlyList<ActivityDto> CardActivity,
    IReadOnlyList<AccountEntryDto> AccountActivity);

/// <summary>Rendered wording on the wire.</summary>
/// <param name="Version"><c>v1</c>.</param>
/// <param name="Heading">Heading.</param>
/// <param name="Lines">Lines in display order.</param>
/// <param name="Button">Button label.</param>
public sealed record WordingDto(string Version, string Heading, IReadOnlyList<string> Lines, string Button);

/// <summary><c>POST /api/cardholder/cards/{id}/quotes</c> 201 body.</summary>
/// <param name="Reference">New reference.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Fee">Fee.</param>
/// <param name="Total">Total.</param>
/// <param name="From">Source label.</param>
/// <param name="To">Destination label.</param>
/// <param name="Wording">W-1 as rendered.</param>
/// <param name="CreatedAt">Quote time.</param>
/// <param name="Origin">Origin wire name.</param>
public sealed record QuoteDto(string Reference, string Amount, string Fee, string Total, string From, string To, WordingDto Wording, DateTimeOffset CreatedAt, string Origin);

/// <summary>
/// The payment-specific fields of a 409 ProblemDetails (design §7): <c>code</c> (snake_case), <c>message</c> (§17 string),
/// <c>balanceOwed</c>, and for <c>payment_pending</c> <c>pendingReference</c> + <c>amount</c>. Read by tests from the raw body.
/// </summary>
/// <param name="Code"><c>nothing_to_pay</c> | <c>above_limit</c> | <c>payment_pending</c> | <c>balance_changed</c> | <c>quote_refused</c>.</param>
/// <param name="Message">The W-* text.</param>
/// <param name="BalanceOwed">Balance owed.</param>
/// <param name="PendingReference">Pending payment reference (payment_pending only).</param>
/// <param name="Amount">Pending payment amount (payment_pending only).</param>
public sealed record PaymentProblemDto(string Code, string? Message, string? BalanceOwed, string? PendingReference, string? Amount);

/// <summary>Fake bank account part.</summary>
/// <param name="LastFour">Last four digits.</param>
/// <param name="AvailableBalance">Available balance.</param>
public sealed record FakeBankAccountDto(string LastFour, string AvailableBalance);

/// <summary><c>GET /api/fake-bank</c> and <c>PUT /api/fake-bank/steering</c> body.</summary>
/// <param name="NextDebitOutcome"><c>complete</c> | <c>refuse_insufficient_funds</c> | <c>refuse_account_unavailable</c> | <c>timeout_debit_applied</c> | <c>timeout_not_received</c> (unset shows <c>complete</c>).</param>
/// <param name="NextStatusCheck"><c>answers</c> | <c>does_not_answer</c> (unset shows <c>answers</c>).</param>
/// <param name="Account">Account.</param>
/// <param name="Entries">Ledger entries, newest first (last 10).</param>
public sealed record FakeBankDto(string NextDebitOutcome, string NextStatusCheck, FakeBankAccountDto Account, IReadOnlyList<AccountEntryDto> Entries);

/// <summary><c>PUT /api/fake-bank/steering</c> request; absent fields keep their current value (PLANNER-DECISION).</summary>
/// <param name="NextDebitOutcome">Wire value or null.</param>
/// <param name="NextStatusCheck">Wire value or null.</param>
public sealed record SteeringRequest(string? NextDebitOutcome, string? NextStatusCheck);

/// <summary><c>GET /api/cards/{id}/payments</c> item.</summary>
/// <param name="Reference">Reference.</param>
/// <param name="Status">Quote status: <c>open</c> | <c>confirmed</c> | <c>refused</c>.</param>
/// <param name="State">Payment state, or null when no payment exists.</param>
/// <param name="Amount">Quoted amount.</param>
/// <param name="CreatedAt">Quote time.</param>
/// <param name="SettledAt">Settle time, or null.</param>
/// <param name="FailureReason">Failure reason wire name, or null.</param>
/// <param name="Origin">Origin wire name.</param>
public sealed record PaymentListItemDto(string Reference, string Status, string? State, string Amount, DateTimeOffset CreatedAt, DateTimeOffset? SettledAt, string? FailureReason, string Origin);

/// <summary>The shown part of a record.</summary>
/// <param name="Reference">Reference.</param>
/// <param name="Kind"><c>full</c>.</param>
/// <param name="Amount">Amount shown.</param>
/// <param name="Fee">Fee shown.</param>
/// <param name="Total">Total shown.</param>
/// <param name="From">Source label shown.</param>
/// <param name="To">Destination label shown.</param>
/// <param name="Wording">Rendered wording with version.</param>
/// <param name="CreatedAt">When shown.</param>
public sealed record RecordQuoteDto(string Reference, string Kind, string Amount, string Fee, string Total, string From, string To, WordingDto Wording, DateTimeOffset CreatedAt);

/// <summary>Acting person.</summary>
/// <param name="Kind"><c>cardholder</c> | <c>admin</c>.</param>
/// <param name="Name">Name.</param>
public sealed record ActorDto(string Kind, string Name);

/// <summary>Acceptance part of a record.</summary>
/// <param name="At">Acceptance time: payment creation, else the first refused confirm, else null (open quote) (PLANNER-DECISION).</param>
/// <param name="Actor">Who.</param>
/// <param name="AuthorityBasis">Authority basis.</param>
/// <param name="Origin">Origin wire name.</param>
public sealed record AcceptanceDto(DateTimeOffset? At, ActorDto Actor, string AuthorityBasis, string Origin);

/// <summary>Payment part of a record.</summary>
/// <param name="State">State wire name.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Fee">Fee.</param>
/// <param name="CreatedAt">Acceptance time.</param>
/// <param name="SettledAt">Settle time.</param>
/// <param name="EffectiveAt">Effective time.</param>
/// <param name="BankDebitedAt">Bank debit time.</param>
/// <param name="FailureReason">Failure reason wire name.</param>
/// <param name="BalanceBefore">Balance owed immediately before settlement.</param>
/// <param name="BalanceAfter">Balance owed immediately after settlement.</param>
/// <param name="Simulated">Outcome from fake-bank steering.</param>
public sealed record RecordPaymentDto(string State, string Amount, string Fee, DateTimeOffset CreatedAt, DateTimeOffset? SettledAt, DateTimeOffset? EffectiveAt, DateTimeOffset? BankDebitedAt, string? FailureReason, string? BalanceBefore, string? BalanceAfter, bool Simulated);

/// <summary>One record event.</summary>
/// <param name="At">When.</param>
/// <param name="Kind">Event kind wire name.</param>
/// <param name="From">State before, or null.</param>
/// <param name="To">State after, or null.</param>
/// <param name="Detail">Detail, or null.</param>
/// <param name="Simulated">Steered outcome.</param>
public sealed record PaymentEventDto(DateTimeOffset At, string Kind, string? From, string? To, string? Detail, bool Simulated);

/// <summary><c>GET /api/cards/{id}/payments/{reference}</c> body (AC-20).</summary>
/// <param name="Quote">What was shown.</param>
/// <param name="Status">Quote status.</param>
/// <param name="RefusalReason">Refusal code, or null.</param>
/// <param name="BalanceAtRefusal">Balance at refusal, or null.</param>
/// <param name="Acceptance">Acceptance.</param>
/// <param name="Payment">Payment, or null.</param>
/// <param name="Events">Events oldest first.</param>
public sealed record PaymentRecordDto(RecordQuoteDto Quote, string Status, string? RefusalReason, string? BalanceAtRefusal, AcceptanceDto Acceptance, RecordPaymentDto? Payment, IReadOnlyList<PaymentEventDto> Events);
