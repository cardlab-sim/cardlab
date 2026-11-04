using CardLab.Domain.Controls;
using CardLab.Domain.Limits;

namespace CardLab.Domain.Authorisation;

/// <summary>The pure approve/decline decision (R-3, R-5; design §8).</summary>
/// <remarks>
/// Order: frozen → <see cref="Reason.CardFrozen"/>; else if any limit has
/// <c>spend + amount &gt; limit</c> → <see cref="Reason.LimitExceeded"/> (equal is approved; every limit
/// is evaluated); else <see cref="Reason.Approved"/>. No limits in the category = unlimited.
/// Remaining per limit is computed after the decision: approved → <c>limit - (spend + amount)</c>;
/// declined → <c>limit - spend</c>; floored at £0.00 (spend may already exceed a lowered limit, AC-30).
/// Implemented in PR-04. High-risk path.
/// </remarks>
public static class AuthorisationDecider
{
    /// <summary>Decides one transaction for a known card.</summary>
    /// <param name="status">Card status at decision time (read inside the card lock).</param>
    /// <param name="limitsInCategory">Every limit on the card in the transaction's category, each with the
    /// approved spend in the period containing the transaction timestamp.</param>
    /// <param name="amount">Validated transaction amount.</param>
    /// <returns>The decision; <c>Limits</c> has one entry per input limit, in input order.</returns>
    public static Decision Decide(CardStatus status, IReadOnlyList<LimitWithSpend> limitsInCategory, Money amount) => throw new NotImplementedException("PR-04");
}
