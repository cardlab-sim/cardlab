using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class LimitEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LimitEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-3 · PR-09 · integration
    // Given Field sales with no limits, When PUT fuel/day {"50.00"}, PUT fuel/day {"80.00"}, DELETE fuel/day, Then 200 {fuel, day, "50.00"}, 200 {fuel, day, "80.00"}, 204; GET card limits [] at the end; GET audit newest first: limit_removed (old "Fuel/Day/80.00"), limit_changed (old "Fuel/Day/50.00", new "Fuel/Day/80.00"), limit_set (new "Fuel/Day/50.00")
    // Catches: AC-6/AC-7/AC-8 through the API, duplicate limits, wrong audit order
    [Fact(Skip = "todo")]
    public void Set_change_remove_limit_through_the_api()
    {
    }

    // T-I-3 · PR-09 · integration (theory)
    // Given any card, When PUT fuel/day with "-1.00", "1000000.01", "10.005", "abc", null, or PUT petrol/day / fuel/year with "10.00", Then 400 application/problem+json with errors, and no limit row and no audit row were added
    // Catches: invalid limits stored or audited (AC-9, R-11)
    [Fact(Skip = "todo")]
    public void Invalid_limits_are_400_with_no_side_effects()
    {
    }

    // T-I-3 · PR-09 · integration
    // Given an unknown card id, When PUT and DELETE a limit, Then 404; DELETE of a limit that does not exist on a known card → 204 and no audit
    // Catches: 500 on unknown cards; audit on no-op removal
    [Fact(Skip = "todo")]
    public void Unknown_card_and_absent_limit()
    {
    }
}
