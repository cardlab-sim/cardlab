using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class BothSidesTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public BothSidesTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-8 · PR-08 · integration
    // Given J-1 and J-3 run through the API with complete, refuse_insufficient_funds, refuse_account_unavailable, timeout_debit_applied and timeout_not_received (then checks), When after every step the cardholder view (balance, cardActivity) and GET /api/fake-bank (balance, entries) are read, Then a payment is never reflected on one side only (pending appears only on the card side, not counted), and no fee or charge appears anywhere other than "0.00"
    // Catches: a half-applied payment observable at any step (AC-18, R-9, OB-8)
    [Fact]
    public async Task Both_sides_agree_after_every_step()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        const string opening = "25000.00";

        // J-1: a purchase, a quote (changes nothing), then a completed payment.
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("100.00", "fuel", "Shell", null));
        await AssertSidesAgreeAsync(client, id, owed: "100.00", bankAvailable: opening, "after purchase");
        var first = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await AssertSidesAgreeAsync(client, id, owed: "100.00", bankAvailable: opening, "after quote");
        AssertReceipt(await client.ConfirmAsync(id, first), "completed");
        await AssertSidesAgreeAsync(client, id, owed: "0.00", bankAvailable: "24900.00", "after complete");

        // Refusals move nothing on either side.
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("50.00", "fuel", "Shell", null));
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "after second purchase");
        await client.SetSteeringAsync("refuse_insufficient_funds");
        AssertReceipt(await client.ConfirmAsync(id, (await client.CreateQuoteAsync(id)).Body!.Reference), "failed");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "after insufficient funds");
        await client.SetSteeringAsync("refuse_account_unavailable");
        AssertReceipt(await client.ConfirmAsync(id, (await client.CreateQuoteAsync(id)).Body!.Reference), "failed");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "after account unavailable");

        // J-3: a timeout where the bank never received the debit: pending on the card side only, then failed.
        await client.SetSteeringAsync("timeout_not_received");
        var notReceived = (await client.CreateQuoteAsync(id)).Body!.Reference;
        AssertReceipt(await client.ConfirmAsync(id, notReceived), "pending");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "pending, not received");
        await client.SetSteeringAsync(null, "does_not_answer");
        AssertReceipt(await client.CheckAsync(id, notReceived), "pending");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "check unanswered");
        await client.SetSteeringAsync(null, "answers");
        AssertReceipt(await client.CheckAsync(id, notReceived), "failed");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24900.00", "after not received settled");

        // J-3: a timeout where the bank applied the debit: pending on the card, applied at the bank, then completed.
        await client.SetSteeringAsync("timeout_debit_applied");
        var applied = (await client.CreateQuoteAsync(id)).Body!.Reference;
        AssertReceipt(await client.ConfirmAsync(id, applied), "pending");
        await AssertSidesAgreeAsync(client, id, owed: "50.00", bankAvailable: "24850.00", "pending, debit applied");
        AssertReceipt(await client.CheckAsync(id, applied), "completed");
        await AssertSidesAgreeAsync(client, id, owed: "0.00", bankAvailable: "24850.00", "after debit applied settled");
    }

    private static void AssertReceipt(ApiResponse<ReceiptDto> response, string state)
    {
        Assert.Equal(state, response.Body!.State);
        Assert.Equal("0.00", response.Body.Fee);
    }

    private static async Task AssertSidesAgreeAsync(ApiClient client, Guid id, string owed, string bankAvailable, string step)
    {
        var view = (await client.GetCardholderViewAsync(id)).Body!;
        var bank = (await client.GetFakeBankAsync()).Body!;
        var payments = view.CardActivity.Where(a => a.Kind == "payment").ToList();
        var bankReferences = bank.Entries.Select(e => e.Reference).ToList();

        Assert.True(owed == view.BalanceOwed, $"{step}: balance owed");
        Assert.True(bankAvailable == bank.Account.AvailableBalance, $"{step}: bank available balance");
        Assert.True(bankAvailable == view.Account.AvailableBalance, $"{step}: the view's account balance equals the bank's");
        Assert.True(
            bankReferences.OrderBy(r => r, StringComparer.Ordinal).SequenceEqual(view.AccountActivity.Select(e => e.Reference).OrderBy(r => r, StringComparer.Ordinal)),
            $"{step}: the view's account activity is the bank's ledger");
        foreach (var payment in payments)
        {
            var atBank = bankReferences.Contains(payment.Reference!);
            Assert.True(payment.State switch
            {
                "completed" => atBank,
                "failed" => !atBank,
                _ => true,
            }, $"{step}: {payment.Reference} is {payment.State} on the card but {(atBank ? "present" : "absent")} at the bank");
        }

        foreach (var reference in bankReferences)
        {
            Assert.True(payments.Any(p => p.Reference == reference && p.State is "completed" or "pending"), $"{step}: bank entry {reference} has no completed or pending payment on the card");
        }

        Assert.True(view.CardActivity.All(a => a.Kind is "purchase" or "payment"), $"{step}: an activity row that is neither a purchase nor a payment");
        Assert.True(bank.Entries.All(e => e.Narrative.Length > 0 && !e.Narrative.Contains("fee", StringComparison.OrdinalIgnoreCase) && !e.Narrative.Contains("charge", StringComparison.OrdinalIgnoreCase)), $"{step}: a fee or charge at the bank");
    }
}
