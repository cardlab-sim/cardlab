namespace CardLab.Domain;

/// <summary>The R-11 bounds for transaction and limit amounts.</summary>
/// <remarks>Implemented in PR-01. High-risk path.</remarks>
public static class MoneyBounds
{
    private const decimal MaxAmount = 1_000_000.00m;

    /// <summary>Whether an amount is allowed on a transaction: £0.01 to £1,000,000.00 inclusive.</summary>
    /// <param name="amount">Candidate transaction amount.</param>
    /// <returns>True inside the bounds.</returns>
    public static bool IsValidTransactionAmount(Money amount) => amount.Amount >= 0.01m && amount.Amount <= MaxAmount;

    /// <summary>Whether an amount is allowed as a spend limit: £0.00 to £1,000,000.00 inclusive (£0.00 blocks the category).</summary>
    /// <param name="amount">Candidate limit amount.</param>
    /// <returns>True inside the bounds.</returns>
    public static bool IsValidLimitAmount(Money amount) => amount.Amount <= MaxAmount;
}
