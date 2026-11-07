using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Payments;

public sealed class ConfirmRulesTests
{
    private static readonly Money Quoted = TestMoney.Of("150.50");
    private static readonly PendingPaymentRef Other = new(new PaymentReference("PAY-00000009"), TestMoney.Of("150.50"));

    private static Quote OpenQuote() => new QuoteBuilder().WithAmount("150.50").Build();

    // T-U-6 · PR-01 · unit
    // Given an open £150.50 quote, no payment for it, owed £150.50 and another pending payment PAY-00000009 of £150.50, When Decide, Then RefusePendingExists(PAY-00000009, £150.50)
    // Catches: a second payment in flight on one card (AC-11, R-8)
    [Fact]
    public void Refuses_when_another_payment_is_pending()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Open, null, Other, Quoted);

        Assert.Equal(new ConfirmDecision.RefusePendingExists(new PaymentReference("PAY-00000009"), TestMoney.Of("150.50")), decision);
    }

    // T-U-33 · PR-01 · unit
    // Given an open £150.50 quote: owed £150.50 → Create; own payment in any state (even with owed £170.50 or status Confirmed) → Repeat; owed £170.50 → RefuseBalanceChanged(£170.50); status Refused (even with owed back at £150.50) → RefuseAlreadyRefused(owed); with several conditions true the precedence is Repeat > AlreadyRefused > PendingExists > BalanceChanged
    // Catches: a repeat creating a second payment, or a refused reference paying later (AC-8, AC-9, AC-30, R-5, R-7)
    [Fact]
    public void Creates_when_nothing_blocks_the_confirm()
    {
        Assert.Equal(new ConfirmDecision.Create(), ConfirmRules.Decide(OpenQuote(), QuoteStatus.Open, null, null, Quoted));
    }

    [Theory]
    [InlineData(PaymentState.Pending)]
    [InlineData(PaymentState.Completed)]
    [InlineData(PaymentState.Failed)]
    public void Repeats_when_the_quote_already_has_a_payment_in_any_state(PaymentState state)
    {
        Assert.Equal(
            new ConfirmDecision.Repeat(),
            ConfirmRules.Decide(OpenQuote(), QuoteStatus.Confirmed, state, null, TestMoney.Of("170.50")));
    }

    [Fact]
    public void Refuses_when_the_balance_changed()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Open, null, null, TestMoney.Of("170.50"));

        Assert.Equal(new ConfirmDecision.RefuseBalanceChanged(TestMoney.Of("170.50")), decision);
    }

    [Fact]
    public void Refuses_a_refused_quote_again_even_when_the_balance_is_back()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Refused, null, null, Quoted);

        Assert.Equal(new ConfirmDecision.RefuseAlreadyRefused(Quoted), decision);
    }

    [Fact]
    public void Repeat_wins_over_every_refusal()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Refused, PaymentState.Completed, Other, TestMoney.Of("170.50"));

        Assert.Equal(new ConfirmDecision.Repeat(), decision);
    }

    [Fact]
    public void Already_refused_wins_over_pending_and_changed_balance()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Refused, null, Other, TestMoney.Of("170.50"));

        Assert.Equal(new ConfirmDecision.RefuseAlreadyRefused(TestMoney.Of("170.50")), decision);
    }

    [Fact]
    public void Pending_exists_wins_over_changed_balance()
    {
        var decision = ConfirmRules.Decide(OpenQuote(), QuoteStatus.Open, null, Other, TestMoney.Of("170.50"));

        Assert.Equal(new ConfirmDecision.RefusePendingExists(Other.Reference, Other.Amount), decision);
    }
}
