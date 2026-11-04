using FsCheck.Xunit;

namespace CardLab.Domain.Tests.Properties;

public sealed class PeriodProperties
{
    // T-P-4 · PR-04 · property
    // Given any instant in 2025-2027 (generator biased to ±2 h around 2026-03-29T01:00Z and 2026-10-25T01:00Z), When RangeContaining for Day/Week/Month, Then StartUtc <= t < EndUtcExclusive, the range of EndUtcExclusive starts exactly there (tiling), and LocalStart is the London date of StartUtc
    // Catches: gaps/overlaps at DST, an instant in zero or two periods (AC-25)
    [Property(Skip = "todo")]
    public bool Every_instant_is_in_exactly_one_period_and_periods_tile() => true;

    // T-P-5 · PR-04 · property
    // Given any decimal with at most 2 dp in [0, 1,000,000], When Money.TryFromDecimal then decimal.Parse(ToString(), Invariant), Then the value round-trips
    // Catches: lossy money formatting on the wire (AC-25 "Money round-trips")
    [Property(Skip = "todo")]
    public bool Money_round_trips_through_its_wire_text() => true;
}
