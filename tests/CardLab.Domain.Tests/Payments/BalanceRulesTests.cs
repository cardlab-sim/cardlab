using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Payments;

public sealed class BalanceRulesTests
{
    // T-U-1 · PR-01 · unit
    // Given approved purchases £150.50 (the declined £500.00 is never passed) and completed payments £0.00, When BalanceRules.Owed, Then £150.50; with completed £150.50, Then £0.00; with completed £200.00, Then £0.00 (floor)
    // Catches: declined amounts or a negative balance leaking into Owed (AC-1, AC-2, R-1)
    [Theory]
    [InlineData("150.50", "0.00", "150.50")]
    [InlineData("150.50", "150.50", "0.00")]
    [InlineData("150.50", "200.00", "0.00")]
    public void Owed_is_approved_purchases_minus_completed_payments_floored_at_zero(string approved, string completed, string expected)
    {
        Assert.Equal(TestMoney.Of(expected), BalanceRules.Owed(TestMoney.Of(approved), TestMoney.Of(completed)));
    }

    // T-U-2 · PR-01 · unit
    // Given approved purchases £100.00 and completed payments £0.00 (the failed and pending £100.00 payments are not completed), When Owed, Then £100.00
    // Catches: failed or pending payments counted as paid (AC-3, R-1)
    [Fact]
    public void Failed_and_pending_payments_do_not_reduce_the_balance()
    {
        Assert.Equal(TestMoney.Of("100.00"), BalanceRules.Owed(TestMoney.Of("100.00"), Money.Zero));
    }

    // T-U-4 · PR-01 · unit
    // Given owed £0.00 / £0.01 / £1,000,000.00 / £1,000,000.01 / £150.50 with a pending payment / £1,000,000.01 with a pending payment / £0.00 with a pending payment, When Availability, Then NothingToPay / Available / Available / AboveLimit / PaymentPending / AboveLimit / NothingToPay
    // Catches: off-by-a-penny bounds or wrong precedence (AC-5, AC-24, R-3, R-17)
    [Theory]
    [InlineData("0.00", false, PayAvailability.NothingToPay)]
    [InlineData("0.01", false, PayAvailability.Available)]
    [InlineData("1000000.00", false, PayAvailability.Available)]
    [InlineData("1000000.01", false, PayAvailability.AboveLimit)]
    [InlineData("150.50", true, PayAvailability.PaymentPending)]
    [InlineData("1000000.01", true, PayAvailability.AboveLimit)]
    [InlineData("0.00", true, PayAvailability.NothingToPay)]
    public void Availability_checks_zero_then_limit_then_pending(string owed, bool hasPending, PayAvailability expected)
    {
        Assert.Equal(expected, BalanceRules.Availability(TestMoney.Of(owed), hasPending));
    }
}
