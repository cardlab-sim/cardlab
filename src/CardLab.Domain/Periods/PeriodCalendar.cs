namespace CardLab.Domain.Periods;

/// <summary>
/// Europe/London calendar arithmetic for limit periods (R-4, R-13, DEC-15).
/// </summary>
/// <remarks>
/// Converts the instant to Europe/London (<c>TimeZoneInfo.FindSystemTimeZoneById("Europe/London")</c>,
/// cached), takes the local <see cref="DateOnly"/>, derives the period's first local day (day: itself;
/// week: the Monday on or before it; month: day 1), and converts local 00:00 of that day and of the
/// next period's first day back to UTC. Consecutive periods tile the timeline with no gap or overlap,
/// including the DST changes on 2026-03-29 (23-hour day) and 2026-10-25 (25-hour day).
/// Implemented in PR-04. High-risk path.
/// </remarks>
public static class PeriodCalendar
{
    /// <summary>The Europe/London period of the given kind that contains an instant.</summary>
    /// <param name="instant">Any instant (any offset); the transaction timestamp from the request (R-13).</param>
    /// <param name="period">Day, week or month.</param>
    /// <returns>The range with <c>StartUtc &lt;= instant &lt; EndUtcExclusive</c>.</returns>
    public static PeriodRange RangeContaining(DateTimeOffset instant, Period period) => throw new NotImplementedException("PR-04");
}
