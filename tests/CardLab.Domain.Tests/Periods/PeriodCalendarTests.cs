using System.Globalization;
using CardLab.Domain.Periods;

namespace CardLab.Domain.Tests.Periods;

public sealed class PeriodCalendarTests
{
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static void AssertRange(PeriodRange range, string startUtc, string endUtcExclusive, DateOnly localStart)
    {
        Assert.Equal(Utc(startUtc), range.StartUtc);
        Assert.Equal(Utc(endUtcExclusive), range.EndUtcExclusive);
        Assert.Equal(TimeSpan.Zero, range.StartUtc.Offset);
        Assert.Equal(TimeSpan.Zero, range.EndUtcExclusive.Offset);
        Assert.Equal(localStart, range.LocalStart);
    }

    // T-U-11 · PR-04 · unit
    // Given 2026-10-24T22:30:00Z (23:30 BST, 24 Oct) and 2026-10-24T23:30:00Z (00:30, 25 Oct London), When RangeContaining(…, Day), Then the first is in 24 Oct [2026-10-23T23:00Z, 2026-10-24T23:00Z) and the second in 25 Oct [2026-10-24T23:00Z, 2026-10-26T00:00Z)
    // Catches: periods computed in UTC instead of Europe/London (AC-13)
    [Fact]
    public void Day_boundary_follows_london_midnight_in_summer_time()
    {
        var before = PeriodCalendar.RangeContaining(Utc("2026-10-24T22:30:00Z"), Period.Day);
        var after = PeriodCalendar.RangeContaining(Utc("2026-10-24T23:30:00Z"), Period.Day);

        AssertRange(before, "2026-10-23T23:00:00Z", "2026-10-24T23:00:00Z", new DateOnly(2026, 10, 24));
        AssertRange(after, "2026-10-24T23:00:00Z", "2026-10-26T00:00:00Z", new DateOnly(2026, 10, 25));
    }

    // T-U-12 · PR-04 · unit
    // Given Sunday 2026-11-08 23:00 London and Monday 2026-11-09 00:30 London, When RangeContaining(…, Week), Then different weeks; the second starts Monday 2026-11-09 00:00 London
    // Catches: Sunday-start or ISO-week-year mistakes (AC-14)
    [Fact]
    public void Week_starts_on_monday()
    {
        var sunday = PeriodCalendar.RangeContaining(Utc("2026-11-08T23:00:00Z"), Period.Week);
        var monday = PeriodCalendar.RangeContaining(Utc("2026-11-09T00:30:00Z"), Period.Week);

        AssertRange(sunday, "2026-11-02T00:00:00Z", "2026-11-09T00:00:00Z", new DateOnly(2026, 11, 2));
        AssertRange(monday, "2026-11-09T00:00:00Z", "2026-11-16T00:00:00Z", new DateOnly(2026, 11, 9));
    }

    // T-U-13 · PR-04 · unit
    // Given 2026-11-30 12:00 and 2026-12-01 00:00 London, When RangeContaining(…, Month), Then November and December respectively; December starts exactly at 2026-12-01T00:00Z
    // Catches: inclusive month end (AC-15)
    [Fact]
    public void Month_boundary_is_local_first_of_month()
    {
        var november = PeriodCalendar.RangeContaining(Utc("2026-11-30T12:00:00Z"), Period.Month);
        var december = PeriodCalendar.RangeContaining(Utc("2026-12-01T00:00:00Z"), Period.Month);

        AssertRange(november, "2026-11-01T00:00:00Z", "2026-12-01T00:00:00Z", new DateOnly(2026, 11, 1));
        AssertRange(december, "2026-12-01T00:00:00Z", "2027-01-01T00:00:00Z", new DateOnly(2026, 12, 1));
    }

    // T-U-27 · PR-04 · unit
    // Given 2026-03-29 and 2026-10-25 (DST changes) and the week/month containing them, When RangeContaining, Then the days last 23 h and 25 h, and the week/month ranges start/end on local midnights
    // Catches: fixed 24 h arithmetic across DST (AC-25 boundary part)
    [Fact]
    public void Dst_days_are_23_and_25_hours()
    {
        var spring = Utc("2026-03-29T12:00:00+01:00");
        var autumn = Utc("2026-10-25T12:00:00+00:00");

        var springDay = PeriodCalendar.RangeContaining(spring, Period.Day);
        var autumnDay = PeriodCalendar.RangeContaining(autumn, Period.Day);

        AssertRange(springDay, "2026-03-29T00:00:00Z", "2026-03-29T23:00:00Z", new DateOnly(2026, 3, 29));
        AssertRange(autumnDay, "2026-10-24T23:00:00Z", "2026-10-26T00:00:00Z", new DateOnly(2026, 10, 25));
        Assert.Equal(TimeSpan.FromHours(23), springDay.EndUtcExclusive - springDay.StartUtc);
        Assert.Equal(TimeSpan.FromHours(25), autumnDay.EndUtcExclusive - autumnDay.StartUtc);

        AssertRange(PeriodCalendar.RangeContaining(spring, Period.Week), "2026-03-23T00:00:00Z", "2026-03-29T23:00:00Z", new DateOnly(2026, 3, 23));
        AssertRange(PeriodCalendar.RangeContaining(autumn, Period.Week), "2026-10-18T23:00:00Z", "2026-10-26T00:00:00Z", new DateOnly(2026, 10, 19));
        AssertRange(PeriodCalendar.RangeContaining(spring, Period.Month), "2026-03-01T00:00:00Z", "2026-03-31T23:00:00Z", new DateOnly(2026, 3, 1));
        AssertRange(PeriodCalendar.RangeContaining(autumn, Period.Month), "2026-09-30T23:00:00Z", "2026-11-01T00:00:00Z", new DateOnly(2026, 10, 1));
    }
}
