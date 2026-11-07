namespace CardLab.Domain.Tests.Payments;

public sealed class QuoteRulesTests
{
    // T-U-9 · PR-01 · unit
    // Given Ops van (0001), the seeded linked account (7731), owed £150.50, the cardholder actor Priya Nair, origin DeploySmoke and now, When QuoteRules.Create, Then kind Full, amount £150.50, fee £0.00, total £150.50, FromLabel "Brightline Supplies Ltd current account, ending 7731", ToLabel "Ops van card, ending 0001", the account's authority basis, CreatedAtUtc = now, actor and origin as given, and Shown equal to WordingV1.ReviewScreen(£150.50, FromLabel, ToLabel)
    // Catches: stored fields disagreeing with the wording shown, or a non-zero fee (AC-6, R-4, R-14, OB-9)
    [Fact(Skip = "todo")]
    public void Creates_full_balance_quote_with_zero_fee_and_v1_wording()
    {
    }
}
