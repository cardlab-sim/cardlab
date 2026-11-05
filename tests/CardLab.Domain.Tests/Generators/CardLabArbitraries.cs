using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using FsCheck;
using FsCheck.Fluent;

namespace CardLab.Domain.Tests.Generators;

/// <summary>A decimal with at most two decimal places in [0, 1,000,000] (the wire shape of an amount).</summary>
/// <param name="Value">The amount.</param>
public sealed record TwoDpDecimal(decimal Value);

/// <summary>Limits for one category plus the amounts of a sequence of transactions to decide in order (T-P-1).</summary>
/// <param name="Limits">0-3 limits in one category, distinct periods.</param>
/// <param name="Amounts">1-40 transaction amounts, valid per <see cref="MoneyBounds.IsValidTransactionAmount"/>.</param>
public sealed record LimitScenario(IReadOnlyList<SpendLimit> Limits, IReadOnlyList<Money> Amounts);

/// <summary>An arbitrary decision input: status, limits with arbitrary spend (possibly above a lowered limit) and an amount.</summary>
/// <param name="Status">Active or frozen.</param>
/// <param name="Limits">0-3 limits in one category, distinct periods, each with an approved spend.</param>
/// <param name="Amount">A valid transaction amount.</param>
public sealed record DecisionInput(CardStatus Status, IReadOnlyList<LimitWithSpend> Limits, Money Amount);

/// <summary>
/// FsCheck arbitraries for the property tests (DEC-7): <c>Money</c> (0..1,000,000.00, 2 dp, biased to small values and
/// bounds), <c>Category</c>, <c>Period</c>, limit sets for one category (0-3 limits, distinct periods) and
/// <c>DateTimeOffset</c> instants in 2025-2027 biased to ±2 h around the 2026 DST changes (2026-03-29T01:00Z,
/// 2026-10-25T01:00Z) and to local midnights. Implemented in PR-04 as public static methods returning
/// <c>Arbitrary&lt;T&gt;</c>.
/// </summary>
public static class CardLabArbitraries
{
    private const int MaxCents = 100_000_000;

    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>Wire-shaped decimals: whole pence from 0 to £1,000,000.00, biased to small values and the bounds.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<TwoDpDecimal> TwoDpDecimals() => Arb.From(CentsGen().Select(c => new TwoDpDecimal(c / 100m)));

    /// <summary>Money values, biased to small values and the bounds.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<Money> Amounts() => Arb.From(CentsGen().Select(ToMoney));

    /// <summary>Every category.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<Category> Categories() => Arb.From(Gen.Elements(Enum.GetValues<Category>()));

    /// <summary>Every period.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<Period> Periods() => Arb.From(Gen.Elements(Enum.GetValues<Period>()));

    /// <summary>Limit sets with transaction sequences whose amounts are small enough relative to the limits to produce both approvals and declines.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<LimitScenario> LimitScenarios() =>
        Arb.From(
            from category in Gen.Elements(Enum.GetValues<Category>())
            from limits in LimitSetGen(category)
            from count in Gen.Choose(1, 40)
            from amounts in ListOf(count, TransactionAmountGen())
            select new LimitScenario(limits, amounts));

    /// <summary>Single decisions: any status, any spend (including spend above the limit) and any valid amount.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<DecisionInput> DecisionInputs() =>
        Arb.From(
            from status in Gen.Elements(CardStatus.Active, CardStatus.Frozen)
            from category in Gen.Elements(Enum.GetValues<Category>())
            from limits in LimitSetGen(category)
            from spends in ListOf(limits.Count, CentsGen().Select(ToMoney))
            from amount in TransactionAmountGen()
            select new DecisionInput(status, limits.Zip(spends, (l, s) => new LimitWithSpend(l, s)).ToList(), amount));

    /// <summary>Instants in 2025-2027 with random offsets, biased to ±2 h around the 2026 DST changes and to London midnights.</summary>
    /// <returns>The arbitrary.</returns>
    public static Arbitrary<DateTimeOffset> Instants() => Arb.From(InstantGen());

    private static Gen<int> CentsGen() =>
        Gen.Frequency(
            (5, Gen.Choose(0, 5_000)),
            (2, Gen.Elements(0, 1, 99, 100, 101, MaxCents - 1, MaxCents)),
            (3, Gen.Choose(0, MaxCents)));

    private static Money ToMoney(int cents) =>
        Money.TryFromDecimal(cents / 100m, out var money) ? money : throw new InvalidOperationException("Generator produced an invalid amount.");

    // Valid transaction amounts (>= £0.01), mostly small so that sequences hit the limits.
    private static Gen<Money> TransactionAmountGen() =>
        Gen.Frequency(
            (8, Gen.Choose(1, 3_000)),
            (1, Gen.Choose(1, 100)),
            (1, Gen.Choose(1, MaxCents)))
            .Select(ToMoney);

    private static Gen<IReadOnlyList<SpendLimit>> LimitSetGen(Category category) =>
        from mask in Gen.Choose(0, 7)
        from cents in ListOf(3, LimitCentsGen())
        select (IReadOnlyList<SpendLimit>)Enum.GetValues<Period>()
            .Select((period, i) => (period, i))
            .Where(x => (mask & (1 << x.i)) != 0)
            .Select(x => new SpendLimit(category, x.period, ToMoney(cents[x.i])))
            .ToList();

    private static Gen<int> LimitCentsGen() =>
        Gen.Frequency(
            (7, Gen.Choose(0, 20_000)),
            (1, Gen.Elements(0, 1, MaxCents)),
            (2, Gen.Choose(0, MaxCents)));

    // Builds a list of the given length by sequencing independent draws.
    private static Gen<List<T>> ListOf<T>(int count, Gen<T> item) =>
        count <= 0
            ? Gen.Constant(new List<T>())
            : from head in item
              from tail in ListOf(count - 1, item)
              select new List<T>(tail.Prepend(head));

    private static Gen<DateTimeOffset> InstantGen()
    {
        var from2025 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var to2028 = new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var spring = new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var autumn = new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        const int Window = 2 * 60 * 60;

        var anywhere = Gen.Choose(0, (int)(to2028 - from2025)).Select(s => from2025 + s);
        var aroundSpring = Gen.Choose(-Window, Window).Select(s => spring + s);
        var aroundAutumn = Gen.Choose(-Window, Window).Select(s => autumn + s);

        // Within a few seconds of any London midnight in the range.
        var aroundMidnight =
            from day in Gen.Choose(0, 3 * 365)
            from jitter in Gen.Choose(-3, 3)
            select LondonMidnightUnix(new DateOnly(2025, 1, 1).AddDays(day)) + jitter;

        var offsets = Gen.Elements(TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromHours(-5), TimeSpan.FromMinutes(330), TimeSpan.FromHours(14));

        return from seconds in Gen.Frequency((4, anywhere), (3, aroundSpring), (3, aroundAutumn), (3, aroundMidnight))
               from offset in offsets
               select DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(offset);
    }

    private static long LondonMidnightUnix(DateOnly date) =>
        new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(date, TimeOnly.MinValue, DateTimeKind.Unspecified), London), TimeSpan.Zero).ToUnixTimeSeconds();
}
