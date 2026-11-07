using System.Net;
using System.Text;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class LimitEndpointTests : IClassFixture<ApiFactory>
{
    private static readonly Guid UnknownCard = Guid.Parse("00000000-0000-0000-0000-00000000dead");

    private readonly ApiFactory _factory;

    public LimitEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-3 · PR-09 · integration
    // Given Field sales with no limits, When PUT fuel/day {"50.00"}, PUT fuel/day {"80.00"}, DELETE fuel/day, Then 200 {fuel, day, "50.00"}, 200 {fuel, day, "80.00"}, 204; GET card limits [] at the end; GET audit newest first: limit_removed (old "Fuel/Day/80.00"), limit_changed (old "Fuel/Day/50.00", new "Fuel/Day/80.00"), limit_set (new "Fuel/Day/50.00")
    // Catches: AC-6/AC-7/AC-8 through the API, duplicate limits, wrong audit order
    [Fact]
    public async Task Set_change_remove_limit_through_the_api()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Field sales")).Value;

        var set = await api.SetLimitAsync(id, "fuel", "day", "50.00");

        Assert.Equal(HttpStatusCode.OK, set.Status);
        Assert.Equal(("fuel", "day", "50.00"), (set.Body?.Category, set.Body?.Period, set.Body?.Amount));
        var afterSet = (await api.GetCardAsync(id)).Body;
        var listed = Assert.Single(afterSet!.Limits);
        Assert.Equal(("fuel", "day", "50.00"), (listed.Category, listed.Period, listed.Amount));

        var changed = await api.SetLimitAsync(id, "fuel", "day", "80.00");

        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal(("fuel", "day", "80.00"), (changed.Body?.Category, changed.Body?.Period, changed.Body?.Amount));
        var afterChange = (await api.GetCardAsync(id)).Body;
        Assert.Equal("80.00", Assert.Single(afterChange!.Limits).Amount);

        var removed = await api.RemoveLimitAsync(id, "fuel", "day");

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Empty((await api.GetCardAsync(id)).Body!.Limits);
        var audit = (await api.ListAuditAsync(id)).Body!;
        Assert.Equal(["limit_removed", "limit_changed", "limit_set"], audit.Select(a => a.Action).ToArray());
        Assert.Equal(("Fuel/Day/80.00", null), (audit[0].OldValue, audit[0].NewValue));
        Assert.Equal(("Fuel/Day/50.00", "Fuel/Day/80.00"), (audit[1].OldValue, audit[1].NewValue));
        Assert.Equal((null, "Fuel/Day/50.00"), (audit[2].OldValue, audit[2].NewValue));
    }

    // T-I-3 · PR-09 · integration (theory)
    // Given any card, When PUT fuel/day with "-1.00", "1000000.01", "10.005", "abc", null, or PUT petrol/day / fuel/year with "10.00", Then 400 application/problem+json with errors, and no limit row and no audit row were added
    // Catches: invalid limits stored or audited (AC-9, R-11)
    [Theory]
    [InlineData("fuel", "day", "-1.00", "amount")]
    [InlineData("fuel", "day", "1000000.01", "amount")]
    [InlineData("fuel", "day", "10.005", "amount")]
    [InlineData("fuel", "day", "abc", "amount")]
    [InlineData("fuel", "day", null, "amount")]
    [InlineData("petrol", "day", "10.00", "category")]
    [InlineData("fuel", "year", "10.00", "period")]
    public async Task Invalid_limits_are_400_with_no_side_effects(string category, string period, string? amount, string errorField)
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Marketing")).Value;
        Assert.Equal(HttpStatusCode.OK, (await api.SetLimitAsync(id, "fuel", "day", "50.00")).Status);
        var auditBefore = await _factory.Database.CountAuditEventsAsync();
        var limitsBefore = (await api.GetCardAsync(id)).Body!.Limits.Count;

        var response = await api.SetLimitAsync(id, category, period, amount);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains($"\"{errorField}\":[", response.Raw);
        Assert.Contains("\"errors\":", response.Raw);
        Assert.Equal(auditBefore, await _factory.Database.CountAuditEventsAsync());
        var after = (await api.GetCardAsync(id)).Body!.Limits;
        Assert.Equal(limitsBefore, after.Count);
        Assert.Equal("50.00", after.Single(l => l is { Category: "fuel", Period: "day" }).Amount);
    }

    // T-I-3 · PR-09 review m-2 (BUG-006)
    // Given a body whose amount is a JSON number, When PUT fuel/day, Then 400 application/problem+json, no 16-digit run, and no limit or audit change
    // Catches: unbindable bodies surfacing as 500 (design §7)
    [Fact]
    public async Task Unbindable_body_is_a_400_with_no_side_effects()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Marketing")).Value;
        var auditBefore = await _factory.Database.CountAuditEventsAsync();
        var limitsBefore = (await api.GetCardAsync(id)).Body!.Limits.Count;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/cards/{id}/limits/fuel/day")
        {
            Content = new StringContent("{\"amount\":50}", Encoding.UTF8, "application/json"),
        };

        using var response = await api.SendRawAsync(request);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        PanScanner.AssertNoPan(raw, "400 body");
        Assert.Equal(auditBefore, await _factory.Database.CountAuditEventsAsync());
        Assert.Equal(limitsBefore, (await api.GetCardAsync(id)).Body!.Limits.Count);
    }

    // T-I-3 · PR-09 review m-1
    // Given the R-11 bounds, When PUT 1000000.00 and 0.00, Then both are accepted (inclusive) and the error text names the same maximum
    // Catches: API bound differing from MoneyBounds / R-11
    [Fact]
    public async Task Limit_bounds_are_inclusive()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Ops van")).Value;

        var max = await api.SetLimitAsync(id, "fuel", "week", "1000000.00");
        var zero = await api.SetLimitAsync(id, "fuel", "month", "0.00");
        var over = await api.SetLimitAsync(id, "fuel", "week", "1000000.01");

        Assert.Equal(HttpStatusCode.OK, max.Status);
        Assert.Equal("1000000.00", max.Body?.Amount);
        Assert.Equal(HttpStatusCode.OK, zero.Status);
        Assert.Equal("0.00", zero.Body?.Amount);
        Assert.Equal(HttpStatusCode.BadRequest, over.Status);
        Assert.Contains("1000000.00", over.Raw);
        Assert.Equal("1000000.00", (await api.GetCardAsync(id)).Body!.Limits.Single(l => l is { Category: "fuel", Period: "week" }).Amount);
    }

    // T-I-3 · PR-09 · integration
    // Given an unknown card id, When PUT and DELETE a limit, Then 404; DELETE of a limit that does not exist on a known card → 204 and no audit
    // Catches: 500 on unknown cards; audit on no-op removal
    [Fact]
    public async Task Unknown_card_and_absent_limit()
    {
        var api = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync("Ops van")).Value;
        var auditBefore = await _factory.Database.CountAuditEventsAsync();

        var put = await api.SetLimitAsync(UnknownCard, "fuel", "day", "10.00");
        var delete = await api.RemoveLimitAsync(UnknownCard, "fuel", "day");
        var absent = await api.RemoveLimitAsync(id, "travel", "week");

        Assert.Equal(HttpStatusCode.NotFound, put.Status);
        Assert.Equal(HttpStatusCode.NotFound, delete.Status);
        Assert.Equal(HttpStatusCode.NoContent, absent.Status);
        Assert.Equal(auditBefore, await _factory.Database.CountAuditEventsAsync());
    }
}
