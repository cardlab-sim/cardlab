using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class ConfirmEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public ConfirmEndpointTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-2 · PR-06 · integration
    // Given Ops van owing £150.50, When POST quotes then POST confirm, Then 201 quote with the W-1 lines and 200 receipt {state completed, fee "0.00", message W-2, balanceOwed "0.00", repeated false}; the view then shows balance "0.00", account available "24849.50", an accountActivity entry with the same reference and amount, and a cardActivity row {kind payment, state completed}
    // Catches: money moved on one side only or the payment missing from history (AC-2, AC-7, R-9, R-13)
    [Fact]
    public async Task Full_balance_payment_completes_on_both_sides()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("120.00", "fuel", "Shell", null));
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("30.50", "travel", "Rail Co", null));

        var quote = await client.CreateQuoteAsync(id);

        Assert.Equal(HttpStatusCode.Created, quote.Status);
        Assert.Equal("150.50", quote.Body!.Amount);
        Assert.Equal("0.00", quote.Body.Fee);
        Assert.Equal("Pay your full balance", quote.Body.Wording.Heading);
        Assert.Equal("Amount: £150.50", quote.Body.Wording.Lines[0]);
        Assert.Equal("Confirm payment of £150.50", quote.Body.Wording.Button);
        var reference = quote.Body.Reference;

        var receipt = await client.ConfirmAsync(id, reference);

        Assert.Equal(HttpStatusCode.OK, receipt.Status);
        Assert.Equal("completed", receipt.Body!.State);
        Assert.Equal("0.00", receipt.Body.Fee);
        Assert.Equal("0.00", receipt.Body.BalanceOwed);
        Assert.False(receipt.Body.Repeated);
        Assert.Equal($"Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference {reference}.", receipt.Body.Message);

        var view = (await client.GetCardholderViewAsync(id)).Body!;
        Assert.Equal("0.00", view.BalanceOwed);
        Assert.Equal("24849.50", view.Account.AvailableBalance);
        var entry = Assert.Single(view.AccountActivity);
        Assert.Equal(reference, entry.Reference);
        Assert.Equal("150.50", entry.Amount);
        var payment = Assert.Single(view.CardActivity, row => row.Kind == "payment");
        Assert.Equal("completed", payment.State);
        Assert.Equal(reference, payment.Reference);
    }

    // T-I-2 · PR-06 · integration
    // Given a card owing £25,000.01 and the fake bank at £25,000.00 (no steering), When quote and confirm, Then receipt failed with failureReason insufficient_funds and W-3; balance still "25000.01"; account still "25000.00"
    // Catches: an overdrawing payment accepted (AC-31, R-15)
    [Fact]
    public async Task Debit_above_available_balance_fails_naturally()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        var at = DateTimeOffset.UtcNow;
        await _fixture.Database.SeedTransactionsAsync(TestSeed.OpsVan.Name, [new TransactionBuilder().Amount("25000.01").At(at).DecidedAt(at).Build()]);

        var quote = await client.CreateQuoteAsync(id);
        var receipt = await client.ConfirmAsync(id, quote.Body!.Reference);

        Assert.Equal(HttpStatusCode.OK, receipt.Status);
        Assert.Equal("failed", receipt.Body!.State);
        Assert.Equal("insufficient_funds", receipt.Body.FailureReason);
        Assert.StartsWith("Payment not made. Your bank refused it because the account did not have enough money.", receipt.Body.Message);
        Assert.Equal("25000.01", receipt.Body.BalanceOwed);
        var view = (await client.GetCardholderViewAsync(id)).Body!;
        Assert.Equal("25000.01", view.BalanceOwed);
        Assert.Equal("25000.00", view.Account.AvailableBalance);
    }
}
