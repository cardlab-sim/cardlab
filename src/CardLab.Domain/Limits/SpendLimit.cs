using CardLab.Domain.Periods;

namespace CardLab.Domain.Limits;

/// <summary>A card's maximum approved spend in one category per period (R-4). At most one per (category, period).</summary>
/// <param name="Category">Category the limit applies to.</param>
/// <param name="Period">Period over which spend accumulates.</param>
/// <param name="Amount">Limit, £0.00..£1,000,000.00 (R-11); £0.00 blocks the category.</param>
public sealed record SpendLimit(Category Category, Period Period, Money Amount);

/// <summary>A limit together with the approved spend already made in the period containing the transaction.</summary>
/// <param name="Limit">The limit.</param>
/// <param name="ApprovedSpendInPeriod">Sum of approved transactions of the limit's category in that period.</param>
public sealed record LimitWithSpend(SpendLimit Limit, Money ApprovedSpendInPeriod);

/// <summary>What remains of a limit after a decision (reported in the decision body, R-7).</summary>
/// <param name="Category">Limit category.</param>
/// <param name="Period">Limit period.</param>
/// <param name="Limit">Limit amount.</param>
/// <param name="Remaining"><c>max(0, limit - approved spend after the decision)</c>.</param>
public sealed record LimitRemaining(Category Category, Period Period, Money Limit, Money Remaining);
