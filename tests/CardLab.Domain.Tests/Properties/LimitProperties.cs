using CardLab.Domain.Authorisation;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Tests.Generators;
using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

public sealed class LimitProperties
{
    private static Money Pounds(decimal value) =>
        Money.TryFromDecimal(value, out var money) ? money : throw new InvalidOperationException($"Not a Money: {value}");

    // T-P-1 · PR-04 · property
    // Given any limit set in one category and any sequence of transaction amounts decided in order (approved ones added to spend), When each is decided, Then approved spend per (category, period) never exceeds its limit
    // Catches: any arithmetic or comparison slip in Decide (AC-25 "never exceeds", R-5)
    [Property(Arbitrary = [typeof(CardLabArbitraries)])]
    public bool Approved_spend_never_exceeds_any_limit(LimitScenario scenario)
    {
        // Independent oracle: plain decimal spend per limit, updated only by approvals.
        var spend = new decimal[scenario.Limits.Count];

        foreach (var amount in scenario.Amounts)
        {
            var inputs = scenario.Limits.Select((limit, i) => new LimitWithSpend(limit, Pounds(spend[i]))).ToList();
            var wouldExceed = scenario.Limits.Where((limit, i) => spend[i] + amount.Amount > limit.Amount.Amount).Any();

            var decision = AuthorisationDecider.Decide(CardStatus.Active, inputs, amount);

            // Approved exactly when no limit would be exceeded (an exact fill is approved): rules out both
            // over-approval and over-declining (e.g. always declining would otherwise satisfy "never exceeds").
            if (decision.Outcome == Outcome.Approved == wouldExceed)
            {
                return false;
            }

            if (decision.Outcome == Outcome.Approved)
            {
                for (var i = 0; i < spend.Length; i++)
                {
                    spend[i] += amount.Amount;
                }
            }

            for (var i = 0; i < spend.Length; i++)
            {
                if (spend[i] > scenario.Limits[i].Amount.Amount)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // T-P-2 · PR-04 · property
    // Given any state and amount, When the decision is declined, Then every reported remaining equals limit - spend (unchanged by the amount)
    // Catches: declines changing spend (AC-25)
    [Property(Arbitrary = [typeof(CardLabArbitraries)])]
    public bool Declines_never_change_spend(DecisionInput input)
    {
        var decision = AuthorisationDecider.Decide(input.Status, input.Limits, input.Amount);

        if (decision.Outcome != Outcome.Declined)
        {
            return true;
        }

        return decision.Limits.Count == input.Limits.Count
            && input.Limits.Select((l, i) =>
                decision.Limits[i].Remaining.Amount == Math.Max(0m, l.Limit.Amount.Amount - l.ApprovedSpendInPeriod.Amount)).All(ok => ok);
    }

    // T-P-3 · PR-04 · property
    // Given any state and amount, When decided, Then remaining = max(0, limit - spendAfter) where spendAfter = spend + amount if approved else spend
    // Catches: remaining formula drift (AC-25)
    [Property(Arbitrary = [typeof(CardLabArbitraries)])]
    public bool Remaining_is_limit_minus_approved_spend_floored(DecisionInput input)
    {
        var decision = AuthorisationDecider.Decide(input.Status, input.Limits, input.Amount);
        var approved = decision.Outcome == Outcome.Approved;

        return decision.Limits.Count == input.Limits.Count
            && input.Limits.Select((l, i) =>
            {
                var spendAfter = l.ApprovedSpendInPeriod.Amount + (approved ? input.Amount.Amount : 0m);
                var reported = decision.Limits[i];
                return reported.Category == l.Limit.Category
                    && reported.Period == l.Limit.Period
                    && reported.Limit == l.Limit.Amount
                    && reported.Remaining.Amount == Math.Max(0m, l.Limit.Amount.Amount - spendAfter);
            }).All(ok => ok);
    }
}
