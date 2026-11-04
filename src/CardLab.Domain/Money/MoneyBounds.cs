namespace CardLab.Domain;

/// <summary>The R-11 bounds for transaction and limit amounts.</summary>
/// <remarks>Implemented in PR-01. High-risk path.</remarks>
public static class MoneyBounds
{
    /// <summary>Whether an amount is allowed on a transaction: £0.01 to £1,000,000.00 inclusive.</summary>
    /// <param name="amount">Candidate transaction amount.</param>
    /// <returns>True inside the bounds.</returns>
    public static bool IsValidTransactionAmount(Money amount) => throw new NotImplementedException("PR-01");

    /// <summary>Whether an amount is allowed as a spend limit: £0.00 to £1,000,000.00 inclusive (£0.00 blocks the category).</summary>
    /// <param name="amount">Candidate limit amount.</param>
    /// <returns>True inside the bounds.</returns>
    public static bool IsValidLimitAmount(Money amount) => throw new NotImplementedException("PR-01");
}
