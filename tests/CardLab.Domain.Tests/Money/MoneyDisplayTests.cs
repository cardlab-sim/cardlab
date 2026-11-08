using System.Globalization;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.MoneyRules;

public sealed class MoneyDisplayTests
{
    // T-U-34 · PR-01 · unit
    // Given 0.00, 0.5, 150.50, 1234.56, 25000.01, 1000000.00 and 1000000.01, When ToDisplay (also with CurrentCulture de-DE), Then "£0.00", "£0.50", "£150.50", "£1,234.56", "£25,000.01", "£1,000,000.00", "£1,000,000.01"
    // Catches: culture-dependent or ungrouped amounts in the wording (NFR-5, DEC-10)
    [Theory]
    [InlineData("0", "£0.00")]
    [InlineData("0.5", "£0.50")]
    [InlineData("150.50", "£150.50")]
    [InlineData("1234.56", "£1,234.56")]
    [InlineData("25000.01", "£25,000.01")]
    [InlineData("1000000.00", "£1,000,000.00")]
    [InlineData("1000000.01", "£1,000,000.01")]
    public void ToDisplay_formats_pounds_with_grouping_and_two_decimals(string amount, string expected)
    {
        var money = TestMoney.Of(amount);

        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            Assert.Equal(expected, money.ToDisplay());
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(expected, money.ToDisplay());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
