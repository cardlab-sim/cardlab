using System.Globalization;
using System.Text.RegularExpressions;
using CardLab.Domain.Periods;
using CardLab.Domain.Tests.Generators;
using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

public sealed class PeriodProperties
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    // T-P-4 · PR-04 · property
    // Given any instant in 2025-2027 (generator biased to ±2 h around 2026-03-29T01:00Z and 2026-10-25T01:00Z), When RangeContaining for Day/Week/Month, Then StartUtc <= t < EndUtcExclusive, the range of EndUtcExclusive starts exactly there (tiling), and LocalStart is the London date of StartUtc
    // Catches: gaps/overlaps at DST, an instant in zero or two periods (AC-25)
    [Property(Arbitrary = [typeof(CardLabArbitraries)])]
    public bool Every_instant_is_in_exactly_one_period_and_periods_tile(DateTimeOffset instant, Period period)
    {
        var range = PeriodCalendar.RangeContaining(instant, period);

        var contains = range.StartUtc <= instant && instant < range.EndUtcExclusive;

        // Tiling: the next period starts exactly where this one ends; the last tick of this one is still in it;
        // the first instant is in it. So no instant falls in a gap or in two periods.
        var next = PeriodCalendar.RangeContaining(range.EndUtcExclusive, period);
        var tilesForward = next.StartUtc == range.EndUtcExclusive && next.EndUtcExclusive > next.StartUtc;
        var lastTick = PeriodCalendar.RangeContaining(range.EndUtcExclusive.AddTicks(-1), period);
        var first = PeriodCalendar.RangeContaining(range.StartUtc, period);
        var closedBothEnds = lastTick == range && first == range;

        // The range starts at London local midnight of its LocalStart date.
        var localStart = TimeZoneInfo.ConvertTime(range.StartUtc, London);
        var startsAtLocalMidnight = localStart.TimeOfDay == TimeSpan.Zero && DateOnly.FromDateTime(localStart.DateTime) == range.LocalStart;
        var localEnd = TimeZoneInfo.ConvertTime(range.EndUtcExclusive, London);
        var endsAtLocalMidnight = localEnd.TimeOfDay == TimeSpan.Zero;

        var offsetsAreZero = range.StartUtc.Offset == TimeSpan.Zero && range.EndUtcExclusive.Offset == TimeSpan.Zero;

        var shapeOk = period switch
        {
            Period.Day => (range.EndUtcExclusive - range.StartUtc) is { } d && d >= TimeSpan.FromHours(23) && d <= TimeSpan.FromHours(25)
                && DateOnly.FromDateTime(localEnd.DateTime) == range.LocalStart.AddDays(1),
            Period.Week => range.LocalStart.DayOfWeek == DayOfWeek.Monday
                && DateOnly.FromDateTime(localEnd.DateTime) == range.LocalStart.AddDays(7),
            Period.Month => range.LocalStart.Day == 1
                && DateOnly.FromDateTime(localEnd.DateTime) == range.LocalStart.AddMonths(1),
            _ => false,
        };

        // The period contains the instant's own London date.
        var instantDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);
        var dateInside = range.LocalStart <= instantDate && instantDate < DateOnly.FromDateTime(localEnd.DateTime);

        return contains && tilesForward && closedBothEnds && startsAtLocalMidnight && endsAtLocalMidnight && offsetsAreZero && shapeOk && dateInside;
    }

    // T-P-5 · PR-04 · property
    // Given any decimal with at most 2 dp in [0, 1,000,000], When Money.TryFromDecimal then decimal.Parse(ToString(), Invariant), Then the value round-trips
    // Catches: lossy money formatting on the wire (AC-25 "Money round-trips")
    [Property(Arbitrary = [typeof(CardLabArbitraries)])]
    public bool Money_round_trips_through_its_wire_text(TwoDpDecimal input)
    {
        if (!Money.TryFromDecimal(input.Value, out var money))
        {
            return false;
        }

        var text = money.ToString();

        return Regex.IsMatch(text, "^[0-9]+\\.[0-9]{2}$")
            && decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) == input.Value
            && money.Amount == input.Value;
    }
}
