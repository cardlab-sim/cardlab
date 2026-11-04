namespace CardLab.Domain.Tests.Periods;

public sealed class PeriodCalendarTests
{
    // T-U-11 · PR-04 · unit
    // Given 2026-10-24T22:30:00Z (23:30 BST, 24 Oct) and 2026-10-24T23:30:00Z (00:30, 25 Oct London), When RangeContaining(…, Day), Then the first is in 24 Oct [2026-10-23T23:00Z, 2026-10-24T23:00Z) and the second in 25 Oct [2026-10-24T23:00Z, 2026-10-26T00:00Z)
    // Catches: periods computed in UTC instead of Europe/London (AC-13)
    [Fact(Skip = "todo")]
    public void Day_boundary_follows_london_midnight_in_summer_time()
    {
    }

    // T-U-12 · PR-04 · unit
    // Given Sunday 2026-11-08 23:00 London and Monday 2026-11-09 00:30 London, When RangeContaining(…, Week), Then different weeks; the second starts Monday 2026-11-09 00:00 London
    // Catches: Sunday-start or ISO-week-year mistakes (AC-14)
    [Fact(Skip = "todo")]
    public void Week_starts_on_monday()
    {
    }

    // T-U-13 · PR-04 · unit
    // Given 2026-11-30 12:00 and 2026-12-01 00:00 London, When RangeContaining(…, Month), Then November and December respectively; December starts exactly at 2026-12-01T00:00Z
    // Catches: inclusive month end (AC-15)
    [Fact(Skip = "todo")]
    public void Month_boundary_is_local_first_of_month()
    {
    }

    // T-U-27 · PR-04 · unit
    // Given 2026-03-29 and 2026-10-25 (DST changes) and the week/month containing them, When RangeContaining, Then the days last 23 h and 25 h, and the week/month ranges start/end on local midnights
    // Catches: fixed 24 h arithmetic across DST (AC-25 boundary part)
    [Fact(Skip = "todo")]
    public void Dst_days_are_23_and_25_hours()
    {
    }
}
