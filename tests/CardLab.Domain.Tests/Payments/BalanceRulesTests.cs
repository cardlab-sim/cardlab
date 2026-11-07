namespace CardLab.Domain.Tests.Payments;

public sealed class BalanceRulesTests
{
    // T-U-1 · PR-01 · unit
    // Given approved purchases £150.50 (the declined £500.00 is never passed) and completed payments £0.00, When BalanceRules.Owed, Then £150.50; with completed £150.50, Then £0.00; with completed £200.00, Then £0.00 (floor)
    // Catches: declined amounts or a negative balance leaking into Owed (AC-1, AC-2, R-1)
    [Fact(Skip = "todo")]
    public void Owed_is_approved_purchases_minus_completed_payments_floored_at_zero()
    {
    }

    // T-U-2 · PR-01 · unit
    // Given approved purchases £100.00 and completed payments £0.00 (the failed and pending £100.00 payments are not completed), When Owed, Then £100.00
    // Catches: failed or pending payments counted as paid (AC-3, R-1)
    [Fact(Skip = "todo")]
    public void Failed_and_pending_payments_do_not_reduce_the_balance()
    {
    }

    // T-U-4 · PR-01 · unit
    // Given owed £0.00 / £0.01 / £1,000,000.00 / £1,000,000.01 / £150.50 with a pending payment / £1,000,000.01 with a pending payment / £0.00 with a pending payment, When Availability, Then NothingToPay / Available / Available / AboveLimit / PaymentPending / AboveLimit / NothingToPay
    // Catches: off-by-a-penny bounds or wrong precedence (AC-5, AC-24, R-3, R-17)
    [Fact(Skip = "todo")]
    public void Availability_checks_zero_then_limit_then_pending()
    {
    }
}
