using System.Diagnostics;
using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class StatusCheckTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public StatusCheckTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-7 · PR-06 · integration
    // Given a pending payment from steering timeout_debit_applied, When POST check (as the SPA does on opening the view), Then 200 completed with time = the bank's debit time, balance "0.00", one BankEntry, and a new quote is allowed
    // Catches: a payment the bank applied staying pending (AC-14, AC-34, R-9)
    [Fact]
    public async Task Check_completes_a_debit_the_bank_applied()
    {
        var client = _fixture.CreateApiClient();
        var (id, reference) = await PendingAsync(client, "timeout_debit_applied");

        var checkedPayment = await client.CheckAsync(id, reference);

        Assert.Equal(HttpStatusCode.OK, checkedPayment.Status);
        Assert.Equal("completed", checkedPayment.Body!.State);
        Assert.Equal("0.00", checkedPayment.Body.BalanceOwed);
        var bank = (await client.GetFakeBankAsync()).Body!;
        var entry = Assert.Single(bank.Entries);
        Assert.Equal(reference, entry.Reference);
        Assert.True((checkedPayment.Body.Time - entry.PostedAt).Duration() < TimeSpan.FromMilliseconds(1), "the receipt time is the bank's debit time");
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("10.00", "fuel", "Shell", null));
        var next = await client.CreateQuoteAsync(id);
        Assert.Equal(HttpStatusCode.Created, next.Status);
        Assert.Equal("10.00", next.Body!.Amount);
    }

    // T-I-7 · PR-06 · integration
    // Given a pending payment from steering timeout_not_received, When POST check, Then 200 failed with failureReason not_received and the W-4 text, balance "150.50", no BankEntry, and a new quote is allowed
    // Catches: a never-received debit left pending (AC-15, R-11)
    [Fact]
    public async Task Check_fails_a_debit_the_bank_never_received()
    {
        var client = _fixture.CreateApiClient();
        var (id, reference) = await PendingAsync(client, "timeout_not_received");

        var checkedPayment = await client.CheckAsync(id, reference);

        Assert.Equal(HttpStatusCode.OK, checkedPayment.Status);
        Assert.Equal("failed", checkedPayment.Body!.State);
        Assert.Equal("not_received", checkedPayment.Body.FailureReason);
        Assert.Equal($"Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference {reference}.", checkedPayment.Body.Message);
        Assert.Equal("150.50", checkedPayment.Body.BalanceOwed);
        Assert.Equal(0, await _fixture.Database.CountRowsAsync("BankEntry"));
        var next = await client.CreateQuoteAsync(id);
        Assert.Equal(HttpStatusCode.Created, next.Status);
    }

    // T-I-7 · PR-06 · integration
    // Given a pending payment and nextStatusCheck does_not_answer, When POST check, Then 200 pending with W-10 within 3 s; the bank received no new debit request; a second check answers
    // Catches: unanswered checks failing or re-sending (AC-16, R-11)
    [Fact]
    public async Task Unanswered_check_stays_pending()
    {
        var client = _fixture.CreateApiClient();
        var (id, reference) = await PendingAsync(client, "timeout_not_received");
        await client.SetSteeringAsync(null, "does_not_answer");
        var started = Stopwatch.GetTimestamp();

        var unanswered = await client.CheckAsync(id, reference);

        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(3), "an unanswered check must return within the bank budget plus margin");
        Assert.Equal(HttpStatusCode.OK, unanswered.Status);
        Assert.Equal("pending", unanswered.Body!.State);
        Assert.Equal($"Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference {reference}.", unanswered.Body.Message);
        Assert.Equal(0, await _fixture.Database.CountRowsAsync("BankEntry"));
        Assert.Equal(1, await _fixture.Database.CountRowsAsync("Payment"));

        var answered = await client.CheckAsync(id, reference);

        Assert.Equal("failed", answered.Body!.State);
    }

    // T-I-7 · PR-06 · integration
    // Given a pending £150.50 payment (timeout_debit_applied) and then a £20.00 approved purchase, When POST check, Then balance "20.00" and the stored record (ICardStore.GetPaymentRecordAsync from a host scope; the admin endpoint is PR-07) shows BalanceBefore £170.50 and BalanceAfter £20.00
    // Catches: settlement balances computed outside the lock (AC-36, R-14)
    [Fact]
    public async Task Settlement_records_before_and_after_balances()
    {
        var client = _fixture.CreateApiClient();
        var (id, reference) = await PendingAsync(client, "timeout_debit_applied");
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("20.00", "fuel", "Shell", null));

        var checkedPayment = await client.CheckAsync(id, reference);

        Assert.Equal("completed", checkedPayment.Body!.State);
        Assert.Equal("20.00", checkedPayment.Body.BalanceOwed);
        using var scope = _fixture.Services.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<ICardStore>().GetPaymentRecordAsync(new PaymentReference(reference), TestContext.Current.CancellationToken);
        var settlement = record!.Payment!.Settlement!;
        Assert.Equal("170.50", settlement.BalanceBefore.ToString());
        Assert.Equal("20.00", settlement.BalanceAfter.ToString());
        Assert.Equal("150.50", record.Payment.Amount.ToString());
    }

    // Catches: a steered no-answer recorded as a real bank non-answer (BUG-010, R-15, AC-20, AC-33)
    [Fact]
    public async Task Steered_no_answer_is_recorded_as_simulated()
    {
        var client = _fixture.CreateApiClient();
        var (id, reference) = await PendingAsync(client, "timeout_not_received");

        var afterConfirm = (await client.GetPaymentRecordAsync(id, reference)).Body!;

        Assert.Equal("bank_unanswered", afterConfirm.Events[^1].Kind);
        Assert.True(afterConfirm.Events[^1].Simulated, "the steered debit no-answer event");
        Assert.True(afterConfirm.Payment!.Simulated, "a pending steered payment shows it was simulated");

        await client.SetSteeringAsync(null, "does_not_answer");
        await client.CheckAsync(id, reference);
        var afterCheck = (await client.GetPaymentRecordAsync(id, reference)).Body!;

        Assert.Equal("status_check_unanswered", afterCheck.Events[^1].Kind);
        Assert.True(afterCheck.Events[^1].Simulated, "the steered status no-answer event");
    }

    private async Task<(Guid Id, string Reference)> PendingAsync(ApiClient client, string steering)
    {
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var reference = (await client.CreateQuoteAsync(id)).Body!.Reference;
        await client.SetSteeringAsync(steering);
        var pending = await client.ConfirmAsync(id, reference);
        Assert.Equal("pending", pending.Body!.State);
        return (id, reference);
    }
}
