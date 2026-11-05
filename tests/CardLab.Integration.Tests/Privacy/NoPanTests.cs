using System.Net;
using System.Text;
using CardLab.Api.Contracts;
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
        _factory.Logs.Clear();
        var bodies = new List<(string Where, string Raw)>();
        ApiResponse<T> Record<T>(string where, ApiResponse<T> response)
        {
            bodies.Add((where, response.Raw));
            return response;
        }

        static void AssertDecision(ApiResponse<DecisionDto> response, string reason)
        {
            Assert.Equal(reason, response.Body?.Reason);
        }

        Assert.Equal("frozen", Record("freeze", await client.FreezeAsync(id)).Body?.Status);
        AssertDecision(Record("authorise frozen", await client.AuthoriseAsync(Request("5.00"))), "card_frozen");
        Assert.Equal("active", Record("unfreeze", await client.UnfreezeAsync(id)).Body?.Status);
        Assert.Equal(HttpStatusCode.OK, Record("set limit", await client.SetLimitAsync(id, "fuel", "day", "50.00")).Status);
        Assert.Equal(HttpStatusCode.OK, Record("change limit", await client.SetLimitAsync(id, "fuel", "day", "30.00")).Status);
        AssertDecision(Record("authorise approved", await client.AuthoriseAsync(Request("20.00"))), "approved");
        AssertDecision(Record("authorise limit_exceeded", await client.AuthoriseAsync(Request("20.00"))), "limit_exceeded");
        AssertDecision(Record("simulate", await client.SimulatePurchaseAsync(id, new SimulatedPurchase("1.00", "fuel", "Shell", "2026-11-04T10:00:00Z"))), "approved");
        Assert.Equal(HttpStatusCode.NoContent, Record("remove limit", await client.RemoveLimitAsync(id, "fuel", "day")).Status);
        AssertDecision(Record("unknown card", await client.AuthoriseAsync(Request("5.00") with { CardNumber = TestSeed.UnknownCardNumber })), "card_not_found");
        var storedBeforeMalformed = await _factory.Database.CountTransactionsAsync();
        AssertDecision(Record("pan in category", await client.AuthoriseAsync(Request("5.00", category: TestSeed.OpsVan.CardNumber))), "invalid_request");
        AssertDecision(Record("pan in merchant", await client.AuthoriseAsync(Request("5.00", merchant: TestSeed.OpsVan.CardNumber))), "invalid_request");
        AssertDecision(Record("spaced pan in merchant", await client.AuthoriseAsync(Request("5.00", merchant: "9999 0001 0000 0001"))), "invalid_request");
        AssertDecision(Record("13-digit merchant", await client.AuthoriseAsync(Request("5.00", merchant: "1234567890123"))), "invalid_request");
        AssertDecision(Record("pan in simulate category", await client.SimulatePurchaseAsync(id, new SimulatedPurchase("5.00", TestSeed.OpsVan.CardNumber, "Shell", "2026-11-04T10:00:00Z"))), "invalid_request");
        bodies.Add(("malformed json", await PostMalformedJsonAsync(client)));
        Assert.Equal(storedBeforeMalformed, await _factory.Database.CountTransactionsAsync());
        Assert.True(storedBeforeMalformed > before);

        var list = await client.ListCardsAsync();
        Record("list cards", list);
        Record("get card", await client.GetCardAsync(id));
        Record("transactions", await client.ListTransactionsAsync(id));
        Record("audit", await client.ListAuditAsync(id));

        foreach (var (where, raw) in bodies)
        {
            PanScanner.AssertNoPan(raw, $"response body: {where}");
        }

        _factory.Logs.AssertNoPan();
        Assert.Equal(10, _factory.Logs.All("AuthorisationDecided").Count);
        Assert.Equal(5, _factory.Logs.All("ControlChanged").Count);
        PanScanner.AssertNoPan(await _factory.Database.DumpAllRowsAsync(), "stored rows");
        foreach (var card in TestSeed.Cards)
        {
            Assert.Contains($"\"lastFour\":\"{card.LastFour}\"", list.Raw);
        }

        Assert.Contains("\"lastFour\":\"0001\"", bodies.Single(b => b.Where == "get card").Raw);
        Assert.DoesNotContain("1234567890123", string.Join(' ', bodies.Select(b => b.Raw)));
        Assert.DoesNotContain("9999 0001 0000 0001", string.Join(' ', bodies.Select(b => b.Raw)));
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
