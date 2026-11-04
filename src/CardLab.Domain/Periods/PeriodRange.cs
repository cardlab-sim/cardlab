namespace CardLab.Domain.Periods;

/// <summary>One concrete Europe/London period as a half-open UTC range <c>[StartUtc, EndUtcExclusive)</c>.</summary>
/// <param name="StartUtc">Local start (00:00 on <paramref name="LocalStart"/>) as a UTC instant (offset zero).</param>
/// <param name="EndUtcExclusive">Local start of the next period as a UTC instant (offset zero).</param>
/// <param name="LocalStart">First local calendar day of the period.</param>
public sealed record PeriodRange(DateTimeOffset StartUtc, DateTimeOffset EndUtcExclusive, DateOnly LocalStart);
