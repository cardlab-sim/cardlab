using System.Globalization;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Logging;

[Collection("sql")]
public sealed class PaymentEventTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public PaymentEventTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-14 · PR-08 · integration
    // Given J-1 (complete) and J-3 (timeout then check) through the API, When the captured OpenTelemetry records are read, Then exactly one PaymentStateChanged per state change (none→pending, pending→completed / failed) with paymentId, cardId, lastFour, kind, amount, fromState, toState, reason, durationMs; BankCallUnanswered for the timeout; no record contains a 16-digit run or "51907731"
    // Catches: missing or duplicated payment events, or numbers in logs (AC-27, NFR-4, NFR-3)
    [Fact]
    public async Task One_structured_event_per_state_change()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        _fixture.Logs.Clear();

        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var j1 = (await client.CreateQuoteAsync(id)).Body!.Reference;
        Assert.Equal("completed", (await client.ConfirmAsync(id, j1)).Body!.State);
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("40.00", "fuel", "Shell", null));
        var j3 = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await client.SetSteeringAsync("timeout_debit_applied");
        Assert.Equal("pending", (await client.ConfirmAsync(id, j3)).Body!.State);
        Assert.Equal("completed", (await client.CheckAsync(id, j3)).Body!.State);

        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("5.00", "fuel", "Shell", null));
        var refused = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await client.SetSteeringAsync("refuse_insufficient_funds");
        Assert.Equal("failed", (await client.ConfirmAsync(id, refused)).Body!.State);

        var changes = _fixture.Logs.All("PaymentStateChanged");
        Assert.Equal(6, changes.Count);
        Assert.Equal("none", changes[4].Attributes["FromState"]);
        Assert.Equal("pending", changes[5].Attributes["FromState"]);
        Assert.Equal("failed", changes[5].Attributes["ToState"]);
        Assert.Equal("insufficient_funds", changes[5].Attributes["Reason"]);
        AssertChange(changes[0], id, "150.50", "none", "pending");
        AssertChange(changes[1], id, "150.50", "pending", "completed");
        AssertChange(changes[2], id, "40.00", "none", "pending");
        AssertChange(changes[3], id, "40.00", "pending", "completed");
        Assert.Equal(changes[0].Attributes["PaymentId"], changes[1].Attributes["PaymentId"]);
        Assert.Equal(changes[2].Attributes["PaymentId"], changes[3].Attributes["PaymentId"]);
        Assert.NotEqual(changes[0].Attributes["PaymentId"], changes[2].Attributes["PaymentId"]);
        var unanswered = Assert.Single(_fixture.Logs.All("BankCallUnanswered"));
        Assert.Equal(j3, unanswered.Attributes["Reference"]);
        Assert.Equal("debit", unanswered.Attributes["Call"]);

        _fixture.Logs.AssertNoPan();
        foreach (var record in _fixture.Logs.Records)
        {
            PanScanner.AssertNoFullNumbers(record.FormattedMessage, $"log message of '{record.EventName}'");
            foreach (var (name, value) in record.Attributes)
            {
                PanScanner.AssertNoFullNumbers(Convert.ToString(value, CultureInfo.InvariantCulture), $"log attribute '{name}' of '{record.EventName}'");
            }
        }
    }

    private static void AssertChange(CapturedRecord record, Guid cardId, string amount, string from, string to)
    {
        foreach (var name in new[] { "PaymentId", "CardId", "LastFour", "Kind", "Amount", "FromState", "ToState", "Reason", "DurationMs" })
        {
            Assert.True(record.Attributes.ContainsKey(name), $"'PaymentStateChanged' record lacks field '{name}'.");
        }

        Assert.True(Guid.TryParse(Convert.ToString(record.Attributes["PaymentId"], CultureInfo.InvariantCulture), out _));
        Assert.Equal(cardId.ToString(), record.Attributes["CardId"]);
        Assert.Equal("0001", record.Attributes["LastFour"]);
        Assert.Equal("full", record.Attributes["Kind"]);
        Assert.Equal(amount, record.Attributes["Amount"]);
        Assert.Equal(from, record.Attributes["FromState"]);
        Assert.Equal(to, record.Attributes["ToState"]);
        Assert.Equal("-", record.Attributes["Reason"]);
        Assert.True(Convert.ToInt64(record.Attributes["DurationMs"], CultureInfo.InvariantCulture) >= 0);
    }
}
