using System.Diagnostics;
using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class TimeoutTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public TimeoutTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-6 · PR-06 · integration
    // Given BankTimeoutSeconds 1 and steering timeout_not_received, When confirm, Then 200 within 3 s with state pending, fee "0.00", message W-5 (balance £150.50); balance still "150.50"; the view shows pendingPayment with W-5 and availability payment_pending
    // Catches: an unanswered bank failing or blocking the confirm (AC-13, NFR-2, R-11)
    [Fact]
    public async Task Unanswered_debit_returns_pending_within_budget()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var reference = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await client.SetSteeringAsync("timeout_not_received");
        var started = Stopwatch.GetTimestamp();

        var receipt = await client.ConfirmAsync(id, reference);

        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(3), "confirm must answer within the bank budget plus margin");
        Assert.Equal(HttpStatusCode.OK, receipt.Status);
        Assert.Equal("pending", receipt.Body!.State);
        Assert.Equal("0.00", receipt.Body.Fee);
        Assert.Equal("150.50", receipt.Body.BalanceOwed);
        const string prefix = "Payment pending. Your bank has not confirmed this payment yet";
        Assert.StartsWith(prefix, receipt.Body.Message);
        Assert.EndsWith($"Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference {reference}.", receipt.Body.Message);

        var view = (await client.GetCardholderViewAsync(id)).Body!;
        Assert.Equal("150.50", view.BalanceOwed);
        Assert.Equal("payment_pending", view.Availability);
        Assert.Equal(receipt.Body.Message, view.PendingPayment!.Message);
        Assert.Equal(reference, view.PendingPayment.Reference);

        var another = await client.CreateQuoteAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, another.Status);
        var problem = another.ProblemAs<PaymentProblemDto>()!;
        Assert.Equal("payment_pending", problem.Code);
        Assert.Equal(reference, problem.PendingReference);
        Assert.Equal("150.50", problem.Amount);
        Assert.Equal("150.50", problem.BalanceOwed);
        Assert.Equal($"A payment of £150.50 (reference {reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.", problem.Message);
    }

    // T-I-6 · PR-06 · integration
    // Given steering timeout_not_received and a resulting pending payment that a check then resolves as failed, When a second payment is quoted and confirmed with no further steering, Then it completes
    // Catches: steering applying to more than one request (AC-32, R-15)
    [Fact]
    public async Task Timeout_steering_applies_to_one_request()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var first = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await client.SetSteeringAsync("timeout_not_received");
        Assert.Equal("pending", (await client.ConfirmAsync(id, first)).Body!.State);
        var checkedFirst = await client.CheckAsync(id, first);
        Assert.Equal("failed", checkedFirst.Body!.State);

        var second = (await client.CreateQuoteAsync(id)).Body!.Reference;
        var receipt = await client.ConfirmAsync(id, second);

        Assert.Equal(HttpStatusCode.OK, receipt.Status);
        Assert.Equal("completed", receipt.Body!.State);
        Assert.Equal("0.00", receipt.Body.BalanceOwed);
    }
}
