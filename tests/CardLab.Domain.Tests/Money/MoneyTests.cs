namespace CardLab.Domain.Tests.MoneyRules;

// Namespace is MoneyRules (not Money) so the type CardLab.Domain.Money is not shadowed.
public sealed class MoneyTests
{
    // T-U-22 · PR-01 · unit
    // Given 10.00, 10.5, 0, 0.01 and 10.005, -1.00, When Money.TryFromDecimal, Then the first four succeed and the last two fail
    // Catches: 3-dp amounts or negatives accepted (R-11, AC-9)
    [Fact(Skip = "todo")]
    public void TryFromDecimal_rejects_more_than_two_decimals_and_negatives()
    {
    }

    // T-U-23 · PR-01 · unit
    // Given 30.00 + 20.00, 20.00 - 30.00, 50.0 vs 50.00, and 1234.5, When added/subtracted/compared/printed, Then 50.00, 0.00 (floored), equal, "1234.50"
    // Catches: negative remaining, scale-sensitive equality, culture-dependent ToString ("1234,50")
    [Fact(Skip = "todo")]
    public void Arithmetic_floors_at_zero_compares_by_value_and_prints_invariant_two_decimals()
    {
    }
}
