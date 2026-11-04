namespace CardLab.Domain.Tests.Generators;

/// <summary>
/// FsCheck arbitraries for the property tests (DEC-7): <c>Money</c> (0..1,000,000.00, 2 dp, biased to small values and
/// bounds), <c>Category</c>, <c>Period</c>, limit sets for one category (0-3 limits, distinct periods) and
/// <c>DateTimeOffset</c> instants in 2025-2027 biased to ±2 h around the 2026 DST changes (2026-03-29T01:00Z,
/// 2026-10-25T01:00Z) and to local midnights. Implemented in PR-04 as public static methods returning
/// <c>Arbitrary&lt;T&gt;</c>.
/// </summary>
public static class CardLabArbitraries
{
}
