using CardLab.Domain.Payments;
using CardLab.Domain.Tests.Generators;
using CardLab.TestKit.Builders;
using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

public sealed class PaymentProperties
{
    private static Money Pounds(decimal value) =>
        Money.TryFromDecimal(value, out var money) ? money : throw new InvalidOperationException($"Not a Money: {value}");

    // T-P-1 · PR-01 · property
    // Given any sequence of approved purchase amounts and completed payment amounts, When BalanceRules.Owed is applied to the running sums, Then it equals max(£0.00, Σpurchases − Σcompleted) and is never negative
    // Catches: balance arithmetic slips for any sequence (AC-25, NFR-1)
    [Property(Arbitrary = [typeof(CardLabArbitraries), typeof(PaymentArbitraries)])]
    public bool Balance_is_purchases_minus_completed_payments_floored(List<Money> purchases, List<Money> completedPayments)
    {
        // Independent oracle: plain decimal sums; Money is only used to feed the rule.
        decimal approved = 0m;
        decimal completed = 0m;
        var steps = purchases.Select(p => (isPurchase: true, amount: p)).Concat(completedPayments.Select(p => (isPurchase: false, amount: p)));
        foreach (var (isPurchase, amount) in steps)
        {
            if (isPurchase)
            {
                approved += amount.Amount;
            }
            else
            {
                completed += amount.Amount;
            }

            var owed = BalanceRules.Owed(Pounds(approved), Pounds(completed));
            if (owed.Amount != Math.Max(0m, approved - completed) || owed.Amount < 0m)
            {
                return false;
            }
        }

        return true;
    }

    // T-P-2 · PR-01 · property
    // Given any ConfirmScript (quotes, confirms with repeated references, purchases in between, settlements) run against a pure model driven by ConfirmRules.Decide and PaymentStateMachine.Apply, When it completes, Then each reference has at most one payment
    // Catches: a repeated confirm creating a second payment (AC-25, R-7)
    [Property(Arbitrary = [typeof(CardLabArbitraries), typeof(PaymentArbitraries)])]
    public bool At_most_one_payment_per_reference(ConfirmScript script)
    {
        var model = new PaymentModel();
        foreach (var step in script.Steps)
        {
            model.Apply(step);
        }

        return model.CreatesPerReference.Values.All(count => count <= 1);
    }

    // T-P-3 · PR-01 · property
    // Given the same model and any ConfirmScript, When each step is applied, Then the card never has more than one pending payment
    // Catches: two payments in flight on one card (AC-25, R-8)
    [Property(Arbitrary = [typeof(CardLabArbitraries), typeof(PaymentArbitraries)])]
    public bool At_most_one_pending_payment_per_card(ConfirmScript script)
    {
        var model = new PaymentModel();
        foreach (var step in script.Steps)
        {
            model.Apply(step);
            if (model.PendingCount > 1)
            {
                return false;
            }
        }

        return true;
    }

    // T-P-4 · PR-01 · property
    // Given any state reached from Pending and any sequence of bank outcomes, When applied in order, Then once Completed or Failed every further Apply fails and the state never changes
    // Catches: terminal states changing (AC-25, AC-17, R-6)
    [Property(Arbitrary = [typeof(CardLabArbitraries), typeof(PaymentArbitraries)])]
    public bool Completed_and_failed_never_change(List<BankOutcome> outcomes)
    {
        var state = PaymentState.Pending;
        foreach (var outcome in outcomes)
        {
            var result = PaymentStateMachine.Apply(state, outcome);
            if (state != PaymentState.Pending)
            {
                if (result.IsOk)
                {
                    return false;
                }
            }
            else if (result.IsOk)
            {
                state = result.Value.To;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    // A pure model of one card's payments, driven only by the domain rules: it stores what the service would store
    // (quotes with their status, payments with their state, the running sums) and records every Create decision.
    private sealed class PaymentModel
    {
        private readonly Dictionary<PaymentReference, (Quote Quote, QuoteStatus Status)> quotes = [];
        private readonly Dictionary<PaymentReference, (PaymentState State, Money Amount)> payments = [];
        private Money approvedPurchases = Pounds(150.50m);
        private Money completedPayments = Money.Zero;

        public Dictionary<PaymentReference, int> CreatesPerReference { get; } = [];

        public int PendingCount => payments.Values.Count(p => p.State == PaymentState.Pending);

        public void Apply(ConfirmStep step)
        {
            switch (step)
            {
                case ConfirmStep.Purchase purchase:
                    approvedPurchases += purchase.Amount;
                    break;
                case ConfirmStep.Quote quote:
                    ApplyQuote(PaymentArbitraries.PoolReference(quote.ReferenceIndex));
                    break;
                case ConfirmStep.Confirm confirm:
                    ApplyConfirm(PaymentArbitraries.PoolReference(confirm.ReferenceIndex));
                    break;
                case ConfirmStep.Bank bank:
                    ApplyBank(bank.Outcome);
                    break;
            }
        }

        private Money Owed => BalanceRules.Owed(approvedPurchases, completedPayments);

        private void ApplyQuote(PaymentReference reference)
        {
            if (quotes.ContainsKey(reference) || BalanceRules.Availability(Owed, PendingCount > 0) != PayAvailability.Available)
            {
                return;
            }

            var quote = new QuoteBuilder().WithReference(reference.Value).WithAmount(Owed.ToString()).Build();
            quotes[reference] = (quote, QuoteStatus.Open);
        }

        private void ApplyConfirm(PaymentReference reference)
        {
            if (!quotes.TryGetValue(reference, out var stored))
            {
                return;
            }

            PaymentState? own = payments.TryGetValue(reference, out var existing) ? existing.State : null;
            var pending = payments.Where(p => p.Key != reference && p.Value.State == PaymentState.Pending).Select(p => new PendingPaymentRef(p.Key, p.Value.Amount)).FirstOrDefault();

            switch (ConfirmRules.Decide(stored.Quote, stored.Status, own, pending, Owed))
            {
                case ConfirmDecision.Create:
                    CreatesPerReference[reference] = CreatesPerReference.GetValueOrDefault(reference) + 1;
                    payments[reference] = (PaymentState.Pending, stored.Quote.Amount);
                    quotes[reference] = (stored.Quote, QuoteStatus.Confirmed);
                    break;
                case ConfirmDecision.RefuseBalanceChanged or ConfirmDecision.RefuseAlreadyRefused:
                    quotes[reference] = (stored.Quote, QuoteStatus.Refused);
                    break;
            }
        }

        private void ApplyBank(BankOutcome outcome)
        {
            var inFlight = payments.Where(p => p.Value.State == PaymentState.Pending).Select(p => p.Key).ToList();
            foreach (var reference in inFlight)
            {
                var result = PaymentStateMachine.Apply(PaymentState.Pending, outcome);
                if (!result.IsOk)
                {
                    continue;
                }

                var amount = payments[reference].Amount;
                payments[reference] = (result.Value.To, amount);
                if (result.Value.To == PaymentState.Completed)
                {
                    completedPayments += amount;
                }
            }
        }
    }
}
