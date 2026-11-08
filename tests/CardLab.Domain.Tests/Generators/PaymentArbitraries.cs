using CardLab.Domain.Payments;
using FsCheck;
using FsCheck.Fluent;

namespace CardLab.Domain.Tests.Generators;

/// <summary>One step of a <see cref="ConfirmScript"/> on a single card.</summary>
public abstract record ConfirmStep
{
    private ConfirmStep()
    {
    }

    /// <summary>Show the review screen: mint a quote for the reference (by index into the small reference pool).</summary>
    /// <param name="ReferenceIndex">Index into the reference pool.</param>
    public sealed record Quote(int ReferenceIndex) : ConfirmStep;

    /// <summary>Confirm the quote with the given reference (a no-op when no such quote exists).</summary>
    /// <param name="ReferenceIndex">Index into the reference pool.</param>
    public sealed record Confirm(int ReferenceIndex) : ConfirmStep;

    /// <summary>An approved purchase lands on the card.</summary>
    /// <param name="Amount">Purchase amount.</param>
    public sealed record Purchase(Money Amount) : ConfirmStep;

    /// <summary>The bank answers for the payment currently in flight (a no-op when none).</summary>
    /// <param name="Outcome">What the bank said.</param>
    public sealed record Bank(BankOutcome Outcome) : ConfirmStep;
}

/// <summary>A sequence of steps on one card (quote, confirm, purchase, bank outcome).</summary>
/// <param name="Steps">The steps, in order.</param>
public sealed record ConfirmScript(IReadOnlyList<ConfirmStep> Steps)
{
    /// <summary>A compact text so that a failing script reads as a counterexample.</summary>
    /// <returns>The steps, one per line.</returns>
    public override string ToString() => string.Join(" | ", Steps.Select(s => s.ToString()));
}

/// <summary>
/// 002 FsCheck arbitraries for the payment properties (design §15.2, T-P-1..T-P-4): <c>PaymentReference</c> (valid
/// format, small pool so repeats are common), <c>PaymentState</c>, <c>BankOutcome</c> (all four kinds, simulated or not)
/// and a <c>ConfirmScript</c> record (a sequence of steps on one card: quote, confirm(reference index), approved purchase
/// (amount), bank outcome for the in-flight payment) that shrinks by dropping steps.
/// </summary>
public static class PaymentArbitraries
{
    /// <summary>Number of distinct references a script can use; small so that repeated references are common.</summary>
    public const int ReferencePoolSize = 3;

    /// <summary>The reference at a pool index.</summary>
    /// <param name="index">Index in [0, <see cref="ReferencePoolSize"/>).</param>
    /// <returns>A valid reference, e.g. <c>PAY-00000001</c>.</returns>
    public static PaymentReference PoolReference(int index) => new($"PAY-0000000{index + 1}");

    /// <summary>References from the small pool.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<PaymentReference> References() => Arb.From(Gen.Choose(0, ReferencePoolSize - 1).Select(PoolReference));

    /// <summary>Every payment state.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<PaymentState> PaymentStates() => Arb.From(Gen.Elements(Enum.GetValues<PaymentState>()));

    /// <summary>All four bank outcome kinds, simulated or not.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<BankOutcome> BankOutcomes() => Arb.From(BankOutcomeGen());

    /// <summary>Scripts of 1-25 steps, shrinking by dropping a step.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<ConfirmScript> ConfirmScripts() =>
        Arb.From(
            from count in Gen.Choose(1, 25)
            from steps in ListOf(count, StepGen())
            select new ConfirmScript(steps),
            ShrinkScript);

    private static Gen<BankOutcome> BankOutcomeGen() =>
        from simulated in Gen.Elements(false, true)
        from outcome in Gen.OneOf(
            Gen.Constant<BankOutcome>(new BankOutcome.Completed(new DateTimeOffset(2026, 11, 7, 10, 5, 0, TimeSpan.Zero), simulated)),
            Gen.Constant<BankOutcome>(new BankOutcome.Completed(null, simulated)),
            Gen.Elements(Enum.GetValues<FailureReason>()).Select(r => (BankOutcome)new BankOutcome.Refused(r, simulated)),
            Gen.Constant<BankOutcome>(new BankOutcome.NotReceived(simulated)),
            Gen.Constant<BankOutcome>(new BankOutcome.Unknown(simulated)))
        select outcome;

    private static Gen<ConfirmStep> StepGen() =>
        Gen.Frequency(
            (3, Gen.Choose(0, ReferencePoolSize - 1).Select(i => (ConfirmStep)new ConfirmStep.Quote(i))),
            (5, Gen.Choose(0, ReferencePoolSize - 1).Select(i => (ConfirmStep)new ConfirmStep.Confirm(i))),
            (2, Gen.Choose(1, 30_000).Select(c => (ConfirmStep)new ConfirmStep.Purchase(Cents(c)))),
            (4, BankOutcomeGen().Select(o => (ConfirmStep)new ConfirmStep.Bank(o))));

    private static Money Cents(int cents) =>
        Money.TryFromDecimal(cents / 100m, out var money) ? money : throw new InvalidOperationException("Generator produced an invalid amount.");

    private static IEnumerable<ConfirmScript> ShrinkScript(ConfirmScript script)
    {
        for (var i = 0; i < script.Steps.Count && script.Steps.Count > 1; i++)
        {
            yield return new ConfirmScript(script.Steps.Where((_, j) => j != i).ToList());
        }
    }

    // Builds a list of the given length by sequencing independent draws.
    private static Gen<List<T>> ListOf<T>(int count, Gen<T> item) =>
        count <= 0
            ? Gen.Constant(new List<T>())
            : from head in item
              from tail in ListOf(count - 1, item)
              select new List<T>(tail.Prepend(head));
}
