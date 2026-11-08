namespace CardLab.Domain.Payments;

/// <summary>
/// Balance arithmetic and pay availability (R-1, R-3, R-8, R-17). Pure; high-risk path (NFR-1, tier 3, T-P-1).
/// </summary>
/// <remarks>Implemented in PR-01. See design §5 and §6 ("Balance owed").</remarks>
public static class BalanceRules
{
    /// <summary>Balance owed: approved purchases minus completed payments, floored at £0.00 (R-1).</summary>
    /// <remarks>Callers pass only approved purchase amounts and only completed payment amounts; declined purchases and
    /// pending or failed payments never reach this method (AC-1, AC-3).</remarks>
    /// <param name="approvedPurchases">Sum of approved purchase amounts on the card.</param>
    /// <param name="completedPayments">Sum of completed payment amounts on the card.</param>
    /// <returns><c>max(£0.00, approvedPurchases − completedPayments)</c>.</returns>
    public static Money Owed(Money approvedPurchases, Money completedPayments) => approvedPurchases - completedPayments;

    /// <summary>Whether a full-balance payment may start, checked in this order (design §5):
    /// £0.00 → <see cref="PayAvailability.NothingToPay"/>; above £1,000,000.00 → <see cref="PayAvailability.AboveLimit"/>;
    /// a pending payment → <see cref="PayAvailability.PaymentPending"/>; else <see cref="PayAvailability.Available"/>.</summary>
    /// <param name="owed">Current balance owed.</param>
    /// <param name="hasPendingPayment">True when the card has a payment in state Pending.</param>
    /// <returns>The availability; £1,000,000.00 exactly is Available (R-17 bounds are inclusive).</returns>
    public static PayAvailability Availability(Money owed, bool hasPendingPayment)
    {
        if (owed == Money.Zero)
        {
            return PayAvailability.NothingToPay;
        }

        if (!MoneyBounds.IsValidTransactionAmount(owed))
        {
            return PayAvailability.AboveLimit;
        }

        return hasPendingPayment ? PayAvailability.PaymentPending : PayAvailability.Available;
    }
}
