using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class ConfirmRefusalTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public ConfirmRefusalTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-3 · PR-06 · integration
    // Given a £150.50 quote and then a £20.00 approved purchase, When confirm, Then 409 ProblemDetails {code balance_changed, message W-6 with £170.50, balanceOwed "170.50"}; no Payment row, no BankEntry; confirming again → 409 {code quote_refused, same message}; a new quote has a different reference
    // Catches: paying a stale amount or reusing a refused reference (AC-8, AC-30, R-5, R-7)
    [Fact]
    public async Task Balance_change_is_refused_and_stays_refused()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("120.00", "fuel", "Shell", null));
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("30.50", "travel", "Rail Co", null));
        var quote = await client.CreateQuoteAsync(id);
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("20.00", "other", "Depot", null));
        const string w6 = "Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.";

        var refused = await client.ConfirmAsync(id, quote.Body!.Reference);

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        var problem = refused.ProblemAs<PaymentProblemDto>()!;
        Assert.Equal("balance_changed", problem.Code);
        Assert.Equal(w6, problem.Message);
        Assert.Equal("170.50", problem.BalanceOwed);
        Assert.Equal(0, await _fixture.Database.CountRowsAsync("Payment"));
        Assert.Equal(0, await _fixture.Database.CountRowsAsync("BankEntry"));

        var again = await client.ConfirmAsync(id, quote.Body.Reference);

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        var repeat = again.ProblemAs<PaymentProblemDto>()!;
        Assert.Equal("quote_refused", repeat.Code);
        Assert.Equal(w6, repeat.Message);
        Assert.Equal(0, await _fixture.Database.CountRowsAsync("Payment"));
        var next = await client.CreateQuoteAsync(id);
        Assert.Equal("170.50", next.Body!.Amount);
        Assert.NotEqual(quote.Body.Reference, next.Body.Reference);
    }

    // T-I-3 · PR-06 · integration
    // Given a card owing £0.00, When POST quotes, Then 409 {code nothing_to_pay, message W-7}; a malformed reference, an unknown reference and another card's reference on confirm/check/receipt → 404
    // Catches: quotes for nothing, or references usable across cards (AC-5, design §9, §10)
    [Fact]
    public async Task Unavailable_quote_and_bad_references_are_rejected()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var opsVan = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        var fieldSales = (await _fixture.Database.CardIdAsync(TestSeed.FieldSales.Name)).Value;

        var nothing = await client.CreateQuoteAsync(opsVan);

        Assert.Equal(HttpStatusCode.Conflict, nothing.Status);
        var problem = nothing.ProblemAs<PaymentProblemDto>()!;
        Assert.Equal("nothing_to_pay", problem.Code);
        Assert.Equal("Nothing to pay. Your card balance is £0.00.", problem.Message);
        Assert.Equal("0.00", problem.BalanceOwed);

        await client.SimulatePurchaseAsync(fieldSales, new SimulatedPurchase("10.00", "fuel", "Shell", null));
        var otherCardsReference = (await client.CreateQuoteAsync(fieldSales)).Body!.Reference;
        foreach (var reference in new[] { "not-a-reference", "pay-abcdefgh", "PAY-UNKNOWN1".Replace("U", "V"), otherCardsReference })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.ConfirmAsync(opsVan, reference)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await client.CheckAsync(opsVan, reference)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetReceiptAsync(opsVan, reference)).Status);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.CreateQuoteAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"))).Status);
    }
}
