using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

// Use [Property(Arbitrary = [typeof(CardLabArbitraries)])] once Generators/CardLabArbitraries.cs exists (PR-04).
public sealed class LimitProperties
{
    // T-P-1 · PR-04 · property
    // Given any limit set in one category and any sequence of transaction amounts decided in order (approved ones added to spend), When each is decided, Then approved spend per (category, period) never exceeds its limit
    // Catches: any arithmetic or comparison slip in Decide (AC-25 "never exceeds", R-5)
    [Property(Skip = "todo")]
    public bool Approved_spend_never_exceeds_any_limit() => true;

    // T-P-2 · PR-04 · property
    // Given any state and amount, When the decision is declined, Then every reported remaining equals limit - spend (unchanged by the amount)
    // Catches: declines changing spend (AC-25)
    [Property(Skip = "todo")]
    public bool Declines_never_change_spend() => true;

    // T-P-3 · PR-04 · property
    // Given any state and amount, When decided, Then remaining = max(0, limit - spendAfter) where spendAfter = spend + amount if approved else spend
    // Catches: remaining formula drift (AC-25)
    [Property(Skip = "todo")]
    public bool Remaining_is_limit_minus_approved_spend_floored() => true;
}
