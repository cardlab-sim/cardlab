namespace CardLab.Domain.Tests.Common;

public sealed class WireNamesTests
{
    // T-U-32 · PR-01 · unit
    // Given every Category, Period, CardStatus, Outcome, Reason and AuditAction, When ToWire then TryParse (category/period), Then the design §7 names (e.g. meals_and_entertainment, limit_changed) round-trip; "Fuel", " fuel", "petrol", null fail
    // Catches: wire-name drift between API, SPA and tests
    [Fact(Skip = "todo")]
    public void Wire_names_match_the_api_contract_and_round_trip()
    {
    }

    // T-U-33 · PR-01 · unit
    // Given "50.00", "20", "-1.00", "10.005", "1e3", "1,000.00", " 5", "", null, When TryParseAmount, Then the first four parse (to 50.00, 20, -1.00, 10.005) and the rest fail
    // Catches: culture-dependent or exponent parsing; rejecting "-1.00" before the bounds check can report it (AC-9)
    [Fact(Skip = "todo")]
    public void Amount_text_parsing_is_plain_invariant_decimal()
    {
    }
}
