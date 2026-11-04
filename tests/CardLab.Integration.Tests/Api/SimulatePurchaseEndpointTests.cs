using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class SimulatePurchaseEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SimulatePurchaseEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-18 · PR-08 · integration
    // Given Ops van, When POST /api/cards/{id}/simulate-purchase {amount "12.34", category "travel", merchant "Rail Co"} without timestamp, Then 200 approved with transactionId, the transaction is first in the stored history (ICardStore.ListTransactionsAsync) with occurredAt ≈ now; an unknown id → 404 card_not_found; amount "abc" → 400 invalid_request with problems [amount]; the response never contains a 16-digit run
    // Catches: simulate path diverging from the processor path or exposing the PAN (DEC-14, AC-22, NFR-1)
    [Fact(Skip = "todo")]
    public void Simulate_purchase_by_card_id()
    {
    }
}
