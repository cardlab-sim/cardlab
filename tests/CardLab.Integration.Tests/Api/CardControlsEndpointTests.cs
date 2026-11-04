using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class CardControlsEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CardControlsEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-1 · PR-09 · integration
    // Given Ops van active, When POST /api/cards/{id}/freeze, Then 200 {status:"frozen"}, GET card shows frozen, GET audit has one event {action "freeze", actorName "Sam Ellis", atUtc ≈ now, oldValue "Active", newValue "Frozen"}; a second freeze → 200 frozen and still one event
    // Catches: endpoint not persisting, or duplicate audit through the API (AC-1, AC-2)
    [Fact(Skip = "todo")]
    public void Freeze_through_the_api()
    {
    }

    // T-I-1 · PR-09 · integration
    // Given Ops van frozen, When POST unfreeze twice, Then 200 {status:"active"} both times and exactly one new "unfreeze" event (old "Frozen", new "Active"); unknown id → 404 ProblemDetails
    // Catches: AC-3/AC-40 through the API; unknown id 500
    [Fact(Skip = "todo")]
    public void Unfreeze_through_the_api()
    {
    }
}
