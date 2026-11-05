using CardLab.Domain.Authorisation;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Authorisation;

public sealed class DeciderTests
{
    private static LimitWithSpend FuelDay(string limit, string spent) =>
        new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of(limit).WithSpend(spent);

    private static LimitWithSpend FuelMonth(string limit, string spent) =>
        new LimitBuilder().For(Category.Fuel).Per(Period.Month).Of(limit).WithSpend(spent);

    private static Decision Decide(CardStatus status, string amount, params LimitWithSpend[] limits) =>
        AuthorisationDecider.Decide(status, limits, TestMoney.Of(amount));

    // T-U-8 · PR-04 · unit
    // Given Active, Fuel/Day £50.00 with £30.00 spent, When Decide(£20.00), Then approved, remaining £0.00
    // Catches: "spend + amount >= limit" declining an exact fill (AC-10)
    [Fact]
    public void Exact_fill_is_approved_with_zero_remaining()
    {
        var decision = Decide(CardStatus.Active, "20.00", FuelDay("50.00", "30.00"));

        Assert.Equal(Outcome.Approved, decision.Outcome);
        Assert.Equal(Reason.Approved, decision.Reason);
        var remaining = Assert.Single(decision.Limits);
        Assert.Equal(new LimitRemaining(Category.Fuel, Period.Day, TestMoney.Of("50.00"), TestMoney.Of("0.00")), remaining);
    }

    // T-U-9 · PR-04 · unit
    // Given the same limit and spend, When Decide(£20.01), Then declined limit_exceeded, remaining £20.00
    // Catches: remaining computed as if the declined amount were spent (AC-11)
    [Fact]
    public void Over_limit_by_a_penny_is_declined_and_remaining_unchanged()
    {
        var decision = Decide(CardStatus.Active, "20.01", FuelDay("50.00", "30.00"));

        Assert.Equal(Outcome.Declined, decision.Outcome);
        Assert.Equal(Reason.LimitExceeded, decision.Reason);
        var remaining = Assert.Single(decision.Limits);
        Assert.Equal(TestMoney.Of("20.00"), remaining.Remaining);
        Assert.Equal(TestMoney.Of("50.00"), remaining.Limit);
    }

    // T-U-10 · PR-04 · unit
    // Given the spend after AC-11 is still £30.00 (declines not counted), When Decide(£20.00), Then approved
    // Catches: declined amounts leaking into spend (AC-12)
    [Fact]
    public void Declines_do_not_count_towards_spend()
    {
        var limits = new[] { FuelDay("50.00", "30.00") };

        var declined = AuthorisationDecider.Decide(CardStatus.Active, limits, TestMoney.Of("20.01"));
        var next = AuthorisationDecider.Decide(CardStatus.Active, limits, TestMoney.Of("20.00"));

        Assert.Equal(Outcome.Declined, declined.Outcome);
        Assert.Equal(Outcome.Approved, next.Outcome);
        Assert.Equal(Reason.Approved, next.Reason);
    }

    // T-U-14 · PR-04 · unit
    // Given Fuel/Day £50.00 with £0.00 spent and Fuel/Month £200.00 with £180.00 spent, When Decide(£30.00), Then declined limit_exceeded and both limits reported
    // Catches: only the first/narrowest limit evaluated (AC-16)
    [Fact]
    public void Every_limit_in_the_category_is_evaluated()
    {
        var decision = Decide(CardStatus.Active, "30.00", FuelDay("50.00", "0.00"), FuelMonth("200.00", "180.00"));

        Assert.Equal(Outcome.Declined, decision.Outcome);
        Assert.Equal(Reason.LimitExceeded, decision.Reason);
        Assert.Collection(
            decision.Limits,
            day => Assert.Equal(new LimitRemaining(Category.Fuel, Period.Day, TestMoney.Of("50.00"), TestMoney.Of("50.00")), day),
            month => Assert.Equal(new LimitRemaining(Category.Fuel, Period.Month, TestMoney.Of("200.00"), TestMoney.Of("20.00")), month));
    }

    // T-U-15 · PR-04 · unit
    // Given Fuel/Day £0.00, When Decide(£0.01), Then declined limit_exceeded
    // Catches: £0.00 treated as "no limit" (AC-17)
    [Fact]
    public void Zero_limit_declines_everything()
    {
        var decision = Decide(CardStatus.Active, "0.01", FuelDay("0.00", "0.00"));

        Assert.Equal(Outcome.Declined, decision.Outcome);
        Assert.Equal(Reason.LimitExceeded, decision.Reason);
    }

    // T-U-16 · PR-04 · unit
    // Given no limits in the transaction's category (the card's Fuel limit is not passed for a Travel purchase), When Decide(£5,000.00), Then approved with an empty limits list
    // Catches: missing limit treated as £0.00 (AC-18)
    [Fact]
    public void No_limit_means_unlimited()
    {
        var decision = Decide(CardStatus.Active, "5000.00");

        Assert.Equal(Outcome.Approved, decision.Outcome);
        Assert.Equal(Reason.Approved, decision.Reason);
        Assert.Empty(decision.Limits);
    }

    // T-U-18 · PR-04 · unit
    // Given Fuel/Day lowered to £30.00 with £40.00 already spent, When Decide(£0.01), Then declined limit_exceeded with remaining £0.00 (not negative)
    // Catches: negative remaining or approval when spend already exceeds a lowered limit (AC-30)
    [Fact]
    public void Lowered_limit_below_spend_declines_with_zero_remaining()
    {
        var decision = Decide(CardStatus.Active, "0.01", FuelDay("30.00", "40.00"));

        Assert.Equal(Outcome.Declined, decision.Outcome);
        Assert.Equal(Reason.LimitExceeded, decision.Reason);
        var remaining = Assert.Single(decision.Limits);
        Assert.Equal(TestMoney.Of("30.00"), remaining.Limit);
        Assert.Equal(TestMoney.Of("0.00"), remaining.Remaining);
    }

    // T-U-26 · PR-04 · unit
    // Given a Frozen card whose limit would also be exceeded, When Decide, Then declined card_frozen and remaining computed without the amount
    // Catches: wrong decision order (frozen must win, design §8; AC-4)
    [Fact]
    public void Frozen_wins_over_limits()
    {
        var decision = Decide(CardStatus.Frozen, "30.00", FuelDay("50.00", "30.00"));

        Assert.Equal(Outcome.Declined, decision.Outcome);
        Assert.Equal(Reason.CardFrozen, decision.Reason);
        var remaining = Assert.Single(decision.Limits);
        Assert.Equal(TestMoney.Of("20.00"), remaining.Remaining);
    }
}
