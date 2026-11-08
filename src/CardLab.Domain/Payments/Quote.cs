using CardLab.Domain.Common;

namespace CardLab.Domain.Payments;

/// <summary>
/// The immutable shown-and-accepted part of a payment (R-4, R-14, OB-9): what the review screen displayed, as
/// structured fields and as rendered wording with its version. Never changed after it is stored.
/// </summary>
/// <remarks>Complete in PR-00. Created only by <see cref="QuoteRules.Create"/>. <see cref="Origin"/> is the
/// PLANNER-DECISION home of DEC-15's marker (design §5 omits it from the record; §6 stores it on <c>PaymentQuote</c>).</remarks>
/// <param name="Reference">The reference minted for this review (R-7).</param>
/// <param name="Card">The card being paid.</param>
/// <param name="Kind">Payment kind (Full in slice 1).</param>
/// <param name="Amount">Amount shown (the balance owed at review time).</param>
/// <param name="Fee">Fee shown (£0.00 in slice 1).</param>
/// <param name="Total">Total shown (= Amount + Fee).</param>
/// <param name="FromLabel">Source shown, e.g. "Brightline Supplies Ltd current account, ending 7731".</param>
/// <param name="ToLabel">Destination shown, e.g. "Ops van card, ending 0001".</param>
/// <param name="Actor">Who was shown it (the card's cardholder).</param>
/// <param name="AuthorityBasis">The standing authority text of the linked account (R-2).</param>
/// <param name="CreatedAtUtc">When the review screen was shown (quote minted), UTC.</param>
/// <param name="Shown">The W-1 wording exactly as rendered (version v1).</param>
/// <param name="Origin">Cardholder view or deploy smoke (DEC-15).</param>
public sealed record Quote(
    PaymentReference Reference,
    CardId Card,
    PaymentKind Kind,
    Money Amount,
    Money Fee,
    Money Total,
    string FromLabel,
    string ToLabel,
    PaymentActor Actor,
    string AuthorityBasis,
    DateTimeOffset CreatedAtUtc,
    RenderedWording Shown,
    PaymentOrigin Origin);

/// <summary>Customer-facing wording exactly as rendered (DEC-10, CD-4): stored verbatim with its version.</summary>
/// <param name="Version">Wording version, <c>"v1"</c> (requirements §17).</param>
/// <param name="Heading">Heading, e.g. "Pay your full balance".</param>
/// <param name="Lines">The lines in display order (W-1: Amount, Fee, Total to pay, When, From, To).</param>
/// <param name="ButtonLabel">Confirm button label, e.g. "Confirm payment of £150.50".</param>
public sealed record RenderedWording(string Version, string Heading, IReadOnlyList<string> Lines, string ButtonLabel);
