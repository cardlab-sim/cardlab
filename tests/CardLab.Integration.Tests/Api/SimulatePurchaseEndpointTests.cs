using System.Net;
using System.Text.RegularExpressions;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class SimulatePurchaseEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SimulatePurchaseEndpointTests(ApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-18 · PR-08 · integration
    // Given Ops van, When POST /api/cards/{id}/simulate-purchase {amount "12.34", category "travel", merchant "Rail Co"} without timestamp, Then 200 approved with transactionId, the transaction is first in the stored history (ICardStore.ListTransactionsAsync) with occurredAt ≈ now; an unknown id → 404 card_not_found; amount "abc" → 400 invalid_request with problems [amount]; the response never contains a 16-digit run
    // Catches: simulate path diverging from the processor path or exposing the PAN (DEC-14, AC-22, NFR-1)
    [Fact]
    public async Task Simulate_purchase_by_card_id()
    {
        var client = _factory.CreateApiClient();
        var id = await _factory.Database.CardIdAsync(TestSeed.OpsVan.Name);
        var sixteenDigits = new Regex("[0-9]{16}");
        var requestedAt = DateTimeOffset.UtcNow;

        var approved = await client.SimulatePurchaseAsync(id.Value, new SimulatedPurchase("12.34", "travel", "Rail Co", null));
        var unknown = await client.SimulatePurchaseAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"), new SimulatedPurchase("12.34", "travel", "Rail Co", null));
        var invalid = await client.SimulatePurchaseAsync(id.Value, new SimulatedPurchase("abc", "travel", "Rail Co", null));

        Assert.Equal(HttpStatusCode.OK, approved.Status);
        Assert.Equal("approved", approved.Body?.Outcome);
        Assert.NotNull(approved.Body?.TransactionId);
        using (var scope = _factory.Services.CreateScope())
        {
            var newest = (await scope.ServiceProvider.GetRequiredService<ICardStore>().ListTransactionsAsync(id, Ct))[0];
            Assert.Equal(approved.Body.TransactionId, newest.Id.Value);
            Assert.InRange((newest.OccurredAt - requestedAt).TotalSeconds, -5, 5);
        }

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal("card_not_found", unknown.Body?.Reason);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Equal("invalid_request", invalid.Body?.Reason);
        Assert.Equal(["amount"], invalid.Body!.Problems.Select(p => p.Field));
        Assert.DoesNotMatch(sixteenDigits, approved.Raw);
        Assert.DoesNotMatch(sixteenDigits, unknown.Raw);
        Assert.DoesNotMatch(sixteenDigits, invalid.Raw);
    }
}
