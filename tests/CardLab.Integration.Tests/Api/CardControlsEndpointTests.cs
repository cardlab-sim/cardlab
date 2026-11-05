using System.Net;
using CardLab.Domain.Controls;
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
    [Fact]
    public async Task Freeze_through_the_api()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Ops van")).Value;
        var before = DateTimeOffset.UtcNow;

        var first = await api.FreezeAsync(id);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal("frozen", first.Body?.Status);
        Assert.Equal("frozen", (await api.GetCardAsync(id)).Body?.Status);
        var audit = (await api.ListAuditAsync(id)).Body;
        Assert.NotNull(audit);
        var only = Assert.Single(audit);
        Assert.Equal("freeze", only.Action);
        Assert.Equal("Sam Ellis", only.ActorName);
        Assert.Equal("Active", only.OldValue);
        Assert.Equal("Frozen", only.NewValue);
        Assert.InRange(only.AtUtc, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));

        var second = await api.FreezeAsync(id);

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal("frozen", second.Body?.Status);
        Assert.Single((await api.ListAuditAsync(id)).Body!);
    }

    // T-I-1 · PR-09 · integration
    // Given Ops van frozen, When POST unfreeze twice, Then 200 {status:"active"} both times and exactly one new "unfreeze" event (old "Frozen", new "Active"); unknown id → 404 ProblemDetails
    // Catches: AC-3/AC-40 through the API; unknown id 500
    [Fact]
    public async Task Unfreeze_through_the_api()
    {
        var api = _factory.CreateApiClient();
        await _factory.Database.SetStatusAsync("Marketing", CardStatus.Frozen);
        var id = (await _factory.Database.CardIdAsync("Marketing")).Value;

        var first = await api.UnfreezeAsync(id);
        var second = await api.UnfreezeAsync(id);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal("active", first.Body?.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal("active", second.Body?.Status);
        Assert.Equal("active", (await api.GetCardAsync(id)).Body?.Status);
        var audit = (await api.ListAuditAsync(id)).Body;
        Assert.NotNull(audit);
        var only = Assert.Single(audit);
        Assert.Equal("unfreeze", only.Action);
        Assert.Equal("Frozen", only.OldValue);
        Assert.Equal("Active", only.NewValue);

        var unknown = await api.UnfreezeAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"));

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Contains("\"title\":\"Card not found\"", unknown.Raw);
        Assert.Equal(HttpStatusCode.NotFound, (await api.FreezeAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"))).Status);
    }
}
