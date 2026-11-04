namespace CardLab.Domain.Tests.Authorisation;

public sealed class DeciderTests
{
    // T-U-8 · PR-04 · unit
    // Given Active, Fuel/Day £50.00 with £30.00 spent, When Decide(£20.00), Then approved, remaining £0.00
    // Catches: "spend + amount >= limit" declining an exact fill (AC-10)
    [Fact(Skip = "todo")]
    public void Exact_fill_is_approved_with_zero_remaining()
    {
    }

    // T-U-9 · PR-04 · unit
    // Given the same limit and spend, When Decide(£20.01), Then declined limit_exceeded, remaining £20.00
    // Catches: remaining computed as if the declined amount were spent (AC-11)
    [Fact(Skip = "todo")]
    public void Over_limit_by_a_penny_is_declined_and_remaining_unchanged()
    {
    }

    // T-U-10 · PR-04 · unit
    // Given the spend after AC-11 is still £30.00 (declines not counted), When Decide(£20.00), Then approved
    // Catches: declined amounts leaking into spend (AC-12)
    [Fact(Skip = "todo")]
    public void Declines_do_not_count_towards_spend()
    {
    }

    // T-U-14 · PR-04 · unit
    // Given Fuel/Day £50.00 with £0.00 spent and Fuel/Month £200.00 with £180.00 spent, When Decide(£30.00), Then declined limit_exceeded and both limits reported
    // Catches: only the first/narrowest limit evaluated (AC-16)
    [Fact(Skip = "todo")]
    public void Every_limit_in_the_category_is_evaluated()
    {
    }

    // T-U-15 · PR-04 · unit
    // Given Fuel/Day £0.00, When Decide(£0.01), Then declined limit_exceeded
    // Catches: £0.00 treated as "no limit" (AC-17)
    [Fact(Skip = "todo")]
    public void Zero_limit_declines_everything()
    {
    }

    // T-U-16 · PR-04 · unit
    // Given no limits in the transaction's category (the card's Fuel limit is not passed for a Travel purchase), When Decide(£5,000.00), Then approved with an empty limits list
    // Catches: missing limit treated as £0.00 (AC-18)
    [Fact(Skip = "todo")]
    public void No_limit_means_unlimited()
    {
    }

    // T-U-18 · PR-04 · unit
    // Given Fuel/Day lowered to £30.00 with £40.00 already spent, When Decide(£0.01), Then declined limit_exceeded with remaining £0.00 (not negative)
    // Catches: negative remaining or approval when spend already exceeds a lowered limit (AC-30)
    [Fact(Skip = "todo")]
    public void Lowered_limit_below_spend_declines_with_zero_remaining()
    {
    }

    // T-U-26 · PR-04 · unit
    // Given a Frozen card whose limit would also be exceeded, When Decide, Then declined card_frozen and remaining computed without the amount
    // Catches: wrong decision order (frozen must win, design §8; AC-4)
    [Fact(Skip = "todo")]
    public void Frozen_wins_over_limits()
    {
    }
}
