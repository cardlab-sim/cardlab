namespace CardLab.Domain.Tests.MoneyRules;

// Namespace is MoneyRules (not Money) so the type CardLab.Domain.Money is not shadowed.
public sealed class MoneyTests
{
    // T-U-22 · PR-01 · unit
    // Given 10.00, 10.5, 0, 0.01 and 10.005, -1.00, When Money.TryFromDecimal, Then the first four succeed and the last two fail
    // Catches: 3-dp amounts or negatives accepted (R-11, AC-9)
    [Theory]
    [InlineData("10.00", true)]
    [InlineData("10.5", true)]
    [InlineData("10.500", true)]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("10.005", false)]
    [InlineData("-1.00", false)]
    public void TryFromDecimal_rejects_more_than_two_decimals_and_negatives(string text, bool expected)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        var ok = Money.TryFromDecimal(value, out var money);

        Assert.Equal(expected, ok);
        if (ok)
        {
            Assert.Equal(value, money.Amount);
            Assert.Equal(2, (decimal.GetBits(money.Amount)[3] >> 16) & 0xFF); // stored at scale 2
        }
        else
        {
            Assert.Equal(Money.Zero, money);
        }
    }

    // T-U-23 · PR-01 · unit
    // Given 30.00 + 20.00, 20.00 - 30.00, 50.0 vs 50.00, and 1234.5, When added/subtracted/compared/printed, Then 50.00, 0.00 (floored), equal, "1234.50"
    // Catches: negative remaining, scale-sensitive equality, culture-dependent ToString ("1234,50")
    [Fact]
    public void Arithmetic_floors_at_zero_compares_by_value_and_prints_invariant_two_decimals()
    {
        var thirty = Of(30.00m);
        var twenty = Of(20.00m);

        Assert.Equal("50.00", (thirty + twenty).ToString());
        Assert.Equal("0.00", (twenty - thirty).ToString());
        Assert.Equal("10.00", (thirty - twenty).ToString());
        Assert.Equal(Of(50.0m), Of(50.00m));
        Assert.Equal(Of(50.0m).GetHashCode(), Of(50.00m).GetHashCode());
        Assert.True(twenty < thirty);
        Assert.True(thirty > twenty);
        Assert.True(twenty <= Of(20.0m));
        Assert.True(twenty >= Of(20.0m));
        Assert.False(twenty >= thirty);
        Assert.True(twenty.CompareTo(thirty) < 0);
        Assert.Equal(thirty, Money.Max(twenty, thirty));

        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1234.50", Of(1234.5m).ToString());
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static Money Of(decimal value)
    {
        Assert.True(Money.TryFromDecimal(value, out var money));
        return money;
    }
}
