using CardLab.Domain.Application;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Application;

public sealed class PaymentStatusCheckTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(PaymentFixture Fixture, PaymentReference Reference)> PendingAsync(bool debitApplied)
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        f.Bank.Hang(debitApplied);
        var pending = await f.ConfirmOkAsync(quote.Reference);
        Assert.Equal(PaymentState.Pending, pending.State);
        return (f, quote.Reference);
    }

    // T-U-16 · PR-04 · unit (application)
    // Given a pending £150.50 payment after Hang(debitApplied: true) at T1, When CheckStatusAsync, Then Completed with EffectiveAt = T1, BalanceOwed £0.00, W-2, a StatusCheckCompleted event, the bank entry still single, and QuoteAsync is allowed again
    // Catches: a payment the bank applied staying unpaid, or the card staying blocked (AC-14, AC-34, R-9, R-11)
    [Fact]
    public async Task Check_settles_completed_with_the_bank_debit_time()
    {
        var (f, reference) = await PendingAsync(debitApplied: true);
        var t1 = PaymentFixture.T0.AddMinutes(1);
        f.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Completed, receipt.State);
        Assert.Equal(t1, receipt.Time);
        Assert.Equal(Money.Zero, receipt.BalanceOwed);
        Assert.Equal("Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference PAY-00000001.", receipt.Message);
        Assert.False(receipt.Repeated);
        var record = await f.RecordAsync(reference);
        Assert.Equal(PaymentEventKind.StatusCheckCompleted, record.Events[^1].Kind);
        var settlement = record.Payment!.Settlement!;
        Assert.Equal(t1, settlement.EffectiveAtUtc);
        Assert.Equal(t1, settlement.BankDebitedAtUtc);
        Assert.Equal(t1.AddMinutes(5), settlement.SettledAtUtc);
        Assert.Single(f.Bank.Entries);
        Assert.Single(f.Bank.DebitRequests);
        Assert.Equal(1, f.Bank.StatusChecks);
        var balance = await f.Store.GetBalanceAsync(f.Card.Id, Ct);
        Assert.Equal(Money.Zero, balance!.Owed);
        var changes = f.Logger.Events("PaymentStateChanged");
        Assert.Equal(2, changes.Count);
        Assert.Equal("completed", changes[1].Fields["ToState"]);

        await f.AddPurchaseAsync("10.00");
        Assert.True((await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct)).IsOk);
    }

    // T-U-17 · PR-04 · unit (application)
    // Given a pending payment after Hang(debitApplied: false), When CheckStatusAsync (bank answers NotReceived), Then Failed with Reason NotReceived and the W-4 text, balance £150.50, a StatusCheckNotReceived event, and the card may pay again
    // Catches: a never-received debit left pending forever or counted as paid (AC-15, R-11)
    [Fact]
    public async Task Check_fails_a_debit_the_bank_never_received()
    {
        var (f, reference) = await PendingAsync(debitApplied: false);

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Failed, receipt.State);
        Assert.Equal(FailureReason.NotReceived, receipt.Reason);
        Assert.Equal(PaymentFixture.W4, receipt.Message);
        Assert.Equal(TestMoney.Of("150.50"), receipt.BalanceOwed);
        var record = await f.RecordAsync(reference);
        Assert.Equal(PaymentEventKind.StatusCheckNotReceived, record.Events[^1].Kind);
        var balance = await f.Store.GetBalanceAsync(f.Card.Id, Ct);
        Assert.Equal(TestMoney.Of("150.50"), balance!.Owed);
        Assert.Empty(f.Bank.Entries);
        Assert.True((await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct)).IsOk);
        var change = f.Logger.Events("PaymentStateChanged")[1];
        Assert.Equal("failed", change.Fields["ToState"]);
        Assert.Equal("not_received", change.Fields["Reason"]);
    }

    [Fact]
    public async Task Check_fails_a_debit_the_bank_refused()
    {
        var (f, reference) = await PendingAsync(debitApplied: false);
        f.Bank.NextStatus(BankOutcomes.InsufficientFunds());

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        Assert.Equal(PaymentState.Failed, result.Value!.State);
        Assert.Equal(FailureReason.InsufficientFunds, result.Value.Reason);
        var record = await f.RecordAsync(reference);
        Assert.Equal(PaymentEventKind.StatusCheckRefused, record.Events[^1].Kind);
    }

    // T-U-18 · PR-04 · unit (application)
    // Given a pending payment and bank.HangStatus(), When CheckStatusAsync, Then still Pending with W-10, a StatusCheckUnanswered event, a BankCallUnanswered log with call "status", and DebitRequests still 1
    // Catches: an unanswered check failing the payment or re-sending the debit (AC-16, R-11)
    [Fact]
    public async Task Unanswered_check_stays_pending_without_a_new_debit()
    {
        var (f, reference) = await PendingAsync(debitApplied: true);
        f.Bank.HangStatus();

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Pending, receipt.State);
        Assert.Equal(PaymentFixture.W10, receipt.Message);
        Assert.Equal(TestMoney.Of("150.50"), receipt.BalanceOwed);
        var record = await f.RecordAsync(reference);
        Assert.Equal(PaymentEventKind.StatusCheckUnanswered, record.Events[^1].Kind);
        Assert.Equal(PaymentState.Pending, record.Payment!.State);
        var unanswered = f.Logger.Events("BankCallUnanswered");
        Assert.Equal(["debit", "status"], unanswered.Select(e => (string)e.Fields["Call"]!).ToArray());
        Assert.Single(f.Bank.DebitRequests);
        Assert.Single(f.Logger.Events("PaymentStateChanged"));
    }

    // T-U-21 · PR-04 · unit (application)
    // Given a pending £150.50 payment on Ops van and then an approved £20.00 purchase, When the bank reports completed and CheckStatusAsync runs, Then BalanceOwed £20.00 and the settlement has amount £150.50, BalanceBefore £170.50 and BalanceAfter £20.00
    // Catches: before/after balances computed from stale sums (AC-36, R-14)
    [Fact]
    public async Task Settlement_records_balances_around_the_settled_amount()
    {
        var (f, reference) = await PendingAsync(debitApplied: true);
        await f.AddPurchaseAsync("20.00");

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        Assert.Equal(PaymentState.Completed, result.Value!.State);
        Assert.Equal(TestMoney.Of("20.00"), result.Value.BalanceOwed);
        var payment = (await f.RecordAsync(reference)).Payment!;
        Assert.Equal(TestMoney.Of("150.50"), payment.Amount);
        Assert.Equal(TestMoney.Of("170.50"), payment.Settlement!.BalanceBefore);
        Assert.Equal(TestMoney.Of("20.00"), payment.Settlement.BalanceAfter);
    }

    // T-U-22 · PR-04 · unit (application)
    // Given a pending payment and NextStatus(Completed(null)), When CheckStatusAsync at clock T2, Then Completed with EffectiveAt = SettledAt = T2 and no BankDebitedAt
    // Catches: a missing bank debit time leaving the effective time empty (AC-38, R-11)
    [Fact]
    public async Task Effective_time_falls_back_to_the_settle_time()
    {
        var (f, reference) = await PendingAsync(debitApplied: false);
        f.Bank.NextStatus(BankOutcomes.Completed(null));
        f.Clock.Advance(TimeSpan.FromMinutes(10));
        var t2 = PaymentFixture.T0.AddMinutes(11);

        var result = await f.Service.CheckStatusAsync(f.Card.Id, reference, Ct);

        Assert.Equal(PaymentState.Completed, result.Value!.State);
        Assert.Equal(t2, result.Value.Time);
        var settlement = (await f.RecordAsync(reference)).Payment!.Settlement!;
        Assert.Equal(t2, settlement.EffectiveAtUtc);
        Assert.Equal(t2, settlement.SettledAtUtc);
        Assert.Null(settlement.BankDebitedAtUtc);
    }

    // T-U-39 · PR-04 · unit (application)
    // Given a completed payment, When CheckStatusAsync, Then the stored receipt, no bank call and no new event; an unknown reference → ReferenceNotFound
    // Catches: status checks re-settling a terminal payment (R-6)
    [Fact]
    public async Task Check_on_a_settled_payment_is_a_no_op()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var original = await f.ConfirmOkAsync(quote.Reference);
        var eventsBefore = (await f.RecordAsync(quote.Reference)).Events.Count;

        var result = await f.Service.CheckStatusAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(original, result.Value);
        Assert.Equal(0, f.Bank.StatusChecks);
        Assert.Equal(eventsBefore, (await f.RecordAsync(quote.Reference)).Events.Count);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.CheckStatusAsync(f.Card.Id, new PaymentReference("PAY-0000ZZZZ"), Ct)).Error);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.CheckStatusAsync(f.Other.Id, quote.Reference, Ct)).Error);
        Assert.IsType<PaymentError.CardNotFound>((await f.Service.CheckStatusAsync(new CardId(Guid.NewGuid()), quote.Reference, Ct)).Error);
    }

    [Fact]
    public async Task Check_on_a_quote_without_a_payment_is_not_found()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();

        var result = await f.Service.CheckStatusAsync(f.Card.Id, quote.Reference, Ct);

        Assert.IsType<PaymentError.ReferenceNotFound>(result.Error);
        Assert.Equal(0, f.Bank.StatusChecks);
    }

    [Fact]
    public async Task Check_waits_for_a_confirm_still_in_flight_instead_of_asking_the_bank()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var gate = new TaskCompletionSource();
        f.Bank.HoldDebitsUntil(gate.Task);

        var confirm = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        var check = f.Service.CheckStatusAsync(f.Card.Id, quote.Reference, Ct);
        Assert.False(check.IsCompleted);
        gate.SetResult();
        await confirm;
        var result = await check;

        Assert.Equal(PaymentState.Completed, result.Value!.State);
        Assert.Equal(0, f.Bank.StatusChecks);
        Assert.Single(f.Bank.DebitRequests);
    }
}
