namespace CardLab.Domain.Tests.MoneyRules;

public sealed class MoneyBoundsTests
{
    // T-U-7 · PR-01 · unit
    // Given limit amounts 0.00, 1000000.00 (valid) and 1000000.01 (invalid; -1.00 and 10.005 are rejected earlier by TryFromDecimal), When IsValidLimitAmount, Then valid/valid/invalid
    // Catches: off-by-one on the inclusive bounds; £0.00 limit wrongly rejected (AC-9, AC-17, R-11)
    [Fact(Skip = "todo")]
    public void Limit_amount_bounds_are_zero_to_one_million_inclusive()
    {
    }

    // T-U-7 · PR-01 · unit
    // Given transaction amounts 0.00 (invalid), 0.01, 1000000.00 (valid), 1000000.01 (invalid), When IsValidTransactionAmount, Then as stated
    // Catches: £0.00 transaction accepted (AC-20) or upper bound exclusive
    [Fact(Skip = "todo")]
    public void Transaction_amount_bounds_are_one_penny_to_one_million_inclusive()
    {
    }
}
