using CardLab.Domain.Controls;

namespace CardLab.Domain.Payments;

/// <summary>Builds the quote shown on the review screen (R-4, R-14). Pure; high-risk path.</summary>
/// <remarks>Implemented in PR-01. Design §5 <c>QuoteRules.Create</c>; DEC-10 (server-rendered wording).</remarks>
public static class QuoteRules
{
    /// <summary>Creates the full-balance quote for a card.</summary>
    /// <remarks>
    /// Kind = Full; Amount = <paramref name="owed"/>; Fee = £0.00; Total = Amount (+ Fee);
    /// FromLabel = <c>WordingV1.Label(account.DisplayName, account.LastFour)</c>;
    /// ToLabel = <c>WordingV1.Label(cardName + " card", card.LastFour)</c> (e.g. "Ops van card, ending 0001");
    /// AuthorityBasis = <c>account.AuthorityBasis</c>; Shown = <c>WordingV1.ReviewScreen(Amount, FromLabel, ToLabel)</c>.
    /// The caller has already checked <c>BalanceRules.Availability</c> is Available.
    /// </remarks>
    /// <param name="reference">Freshly minted reference.</param>
    /// <param name="card">The card (id + last four).</param>
    /// <param name="cardName">Card name, e.g. "Ops van".</param>
    /// <param name="account">The business's linked current account.</param>
    /// <param name="owed">Balance owed now (£0.01..£1,000,000.00).</param>
    /// <param name="actor">The card's cardholder (DEC-9).</param>
    /// <param name="origin">Cardholder view or deploy smoke (DEC-15).</param>
    /// <param name="nowUtc">Quote time, UTC (from <c>IClock</c>).</param>
    /// <returns>The quote to store and show.</returns>
    public static Quote Create(
        PaymentReference reference,
        CardRef card,
        string cardName,
        LinkedAccount account,
        Money owed,
        PaymentActor actor,
        PaymentOrigin origin,
        DateTimeOffset nowUtc) => throw new NotImplementedException("PR-01");
}
