using System.Text;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Privacy;

[Collection("sql")]
public sealed class NoPanTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NoPanTests(ApiFactory factory) => _factory = factory;

    private static AuthorisationRequest Request(string amount, string category = "fuel", string? merchant = "Shell") =>
        new AuthorisationRequestBuilder().WithCardNumber(TestSeed.OpsVan.CardNumber).WithAmount(amount).WithCategory(category).WithMerchant(merchant).WithTimestamp("2026-11-04T10:00:00Z").Build();

    // T-I-7 · PR-11 · integration
    // Given J-1..J-3 run through the API (freeze, authorise frozen, unfreeze, set/change/remove limits, approved and limit_exceeded authorisations, an unknown-card request, malformed requests that put a 16-digit number in the category and merchant fields), When every raw response body, every captured log record (Logs), Database.DumpAllRowsAsync() and every GET list are scanned with PanScanner, Then no 16-digit run appears anywhere and the last four do appear in card responses
    // Catches: PAN leaking via responses, logs, ProblemDetails, stored rows (AC-24, NFR-1)
    [Fact]
    public async Task No_full_card_number_anywhere_after_the_journeys()
    {
        var client = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        var before = await _factory.Database.CountTransactionsAsync();
        var bodies = new List<(string Where, string Raw)>();
        void Record(string where, string raw) => bodies.Add((where, raw));

        Record("freeze", (await client.FreezeAsync(id)).Raw);
        Record("authorise frozen", (await client.AuthoriseAsync(Request("5.00"))).Raw);
        Record("unfreeze", (await client.UnfreezeAsync(id)).Raw);
        Record("set limit", (await client.SetLimitAsync(id, "fuel", "day", "50.00")).Raw);
        Record("change limit", (await client.SetLimitAsync(id, "fuel", "day", "30.00")).Raw);
        Record("authorise approved", (await client.AuthoriseAsync(Request("20.00"))).Raw);
        Record("authorise limit_exceeded", (await client.AuthoriseAsync(Request("20.00"))).Raw);
        Record("simulate", (await client.SimulatePurchaseAsync(id, new SimulatedPurchase("1.00", "fuel", "Shell", "2026-11-04T10:00:00Z"))).Raw);
        Record("remove limit", (await client.RemoveLimitAsync(id, "fuel", "day")).Raw);
        Record("unknown card", (await client.AuthoriseAsync(Request("5.00") with { CardNumber = TestSeed.UnknownCardNumber })).Raw);
        var storedBeforeMalformed = await _factory.Database.CountTransactionsAsync();
        Record("pan in category", (await client.AuthoriseAsync(Request("5.00", category: TestSeed.OpsVan.CardNumber))).Raw);
        Record("pan in merchant", (await client.AuthoriseAsync(Request("5.00", merchant: TestSeed.OpsVan.CardNumber))).Raw);
        Record("spaced pan in merchant", (await client.AuthoriseAsync(Request("5.00", merchant: "9999 0001 0000 0001"))).Raw);
        Record("13-digit merchant", (await client.AuthoriseAsync(Request("5.00", merchant: "1234567890123"))).Raw);
        Record("pan in simulate category", (await client.SimulatePurchaseAsync(id, new SimulatedPurchase("5.00", TestSeed.OpsVan.CardNumber, "Shell", "2026-11-04T10:00:00Z"))).Raw);
        Record("malformed json", await PostMalformedJsonAsync(client));
        Assert.Equal(storedBeforeMalformed, await _factory.Database.CountTransactionsAsync());
        Assert.True(storedBeforeMalformed > before);

        var list = await client.ListCardsAsync();
        Record("list cards", list.Raw);
        Record("get card", (await client.GetCardAsync(id)).Raw);
        Record("transactions", (await client.ListTransactionsAsync(id)).Raw);
        Record("audit", (await client.ListAuditAsync(id)).Raw);

        foreach (var (where, raw) in bodies)
        {
            PanScanner.AssertNoPan(raw, $"response body: {where}");
        }

        _factory.Logs.AssertNoPan();
        Assert.NotEmpty(_factory.Logs.Records);
        PanScanner.AssertNoPan(await _factory.Database.DumpAllRowsAsync(), "stored rows");
        foreach (var card in TestSeed.Cards)
        {
            Assert.Contains($"\"lastFour\":\"{card.LastFour}\"", list.Raw);
        }

        Assert.Contains("\"lastFour\":\"0001\"", bodies.Single(b => b.Where == "get card").Raw);
        Assert.DoesNotContain("1234567890123", string.Join(' ', bodies.Select(b => b.Raw)));
        Assert.DoesNotContain("1234567890123", await _factory.Database.DumpAllRowsAsync());
    }

    private async Task<string> PostMalformedJsonAsync(ApiClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authorisations")
        {
            Content = new StringContent("{\"cardNumber\":\"" + TestSeed.OpsVan.CardNumber + "\",\"amount\":", Encoding.UTF8, "application/json"),
        };
        using var response = await client.SendRawAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
