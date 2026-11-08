using System.Text.RegularExpressions;
using CardLab.Domain.Application;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Application;

public sealed class PaymentServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IReadOnlyList<PaymentEventKind> Kinds(PaymentRecord record) => record.Events.Select(e => e.Kind).ToArray();

    // T-U-25 · PR-04 · unit (application)
    // Given Ops van with approved purchases £150.50 and the seeded linked account, When QuoteAsync(card, DeploySmoke), Then Ok with reference PAY-00000001, W-1 wording for £150.50, actor Cardholder "Priya Nair" with Id = card id, origin DeploySmoke, and the store holds one Open quote; at £0.00 → NothingToPay(W-7), at £1,000,000.01 → AboveLimit(W-8), with a pending payment → PaymentPending(W-9), unknown card → CardNotFound, each storing nothing
    // Catches: quotes stored when payment is unavailable, or the admin recorded as actor (AC-4, AC-5, AC-6, AC-11, AC-24, R-16)
    [Fact]
    public async Task Quote_stores_the_review_or_refuses_with_the_right_message()
    {
        var f = await PaymentFixture.CreateAsync();

        var result = await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.DeploySmoke, Ct);

        Assert.True(result.IsOk);
        var quote = result.Value;
        Assert.Equal(new PaymentReference("PAY-00000001"), quote.Reference);
        Assert.Equal(f.Card.Id, quote.Card);
        Assert.Equal(TestMoney.Of("150.50"), quote.Amount);
        Assert.Equal(Money.Zero, quote.Fee);
        Assert.Equal(TestMoney.Of("150.50"), quote.Total);
        Assert.Equal("Amount: £150.50", quote.Shown.Lines[0]);
        Assert.Equal("From: Brightline Supplies Ltd current account, ending 7731", quote.Shown.Lines[4]);
        Assert.Equal("To: Ops van card, ending 0001", quote.Shown.Lines[5]);
        Assert.Equal("Confirm payment of £150.50", quote.Shown.ButtonLabel);
        Assert.Equal(new PaymentActor(ActorKind.Cardholder, f.Card.Id.Value, "Priya Nair"), quote.Actor);
        Assert.Equal(PaymentOrigin.DeploySmoke, quote.Origin);
        Assert.Equal(PaymentFixture.T0, quote.CreatedAtUtc);
        Assert.Equal(1, f.Store.QuoteCount);
        var stored = await f.Store.GetPaymentRecordAsync(quote.Reference, Ct);
        Assert.Equal(QuoteStatus.Open, stored!.Status);

        var unknown = await f.Service.QuoteAsync(new CardId(Guid.NewGuid()), PaymentOrigin.CardholderView, Ct);
        Assert.IsType<PaymentError.CardNotFound>(unknown.Error);
        Assert.Equal(1, f.Store.QuoteCount);
    }

    [Fact]
    public async Task Quote_is_refused_when_nothing_is_owed()
    {
        var f = await PaymentFixture.CreateAsync(purchases: null);

        var result = await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct);

        var error = Assert.IsType<PaymentError.NothingToPay>(result.Error);
        Assert.Equal("Nothing to pay. Your card balance is £0.00.", error.Message);
        Assert.Equal(Money.Zero, error.BalanceOwed);
        Assert.Equal(0, f.Store.QuoteCount);
    }

    [Fact]
    public async Task Quote_is_refused_above_the_limit()
    {
        var f = await PaymentFixture.CreateAsync(purchases: "1000000.01");

        var result = await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct);

        var error = Assert.IsType<PaymentError.AboveLimit>(result.Error);
        Assert.StartsWith("This balance cannot be paid here. Payments above £1,000,000.00 are not available online.", error.Message);
        Assert.Contains("£1,000,000.01", error.Message);
        Assert.Equal(TestMoney.Of("1000000.01"), error.BalanceOwed);
        Assert.Equal(0, f.Store.QuoteCount);
    }

    // Catches: a receipt or record carrying a sub-millisecond instant that the store (milliseconds) would round, so repeats differ on the wire (BUG-008, AC-9, AC-10)
    [Fact]
    public async Task Instants_reaching_receipt_and_record_are_cut_to_milliseconds()
    {
        var f = await PaymentFixture.CreateAsync();
        f.Clock.Set(PaymentFixture.T0.AddTicks(7777));
        var quote = await f.QuoteOkAsync();
        f.Clock.Set(PaymentFixture.T0.AddTicks(12345));

        var receipt = await f.ConfirmOkAsync(quote.Reference);

        Assert.Equal(PaymentFixture.T0.AddMilliseconds(1), receipt.Time);
        var settlement = (await f.RecordAsync(quote.Reference)).Payment!.Settlement!;
        Assert.Equal(PaymentFixture.T0.AddMilliseconds(1), settlement.SettledAtUtc);
        Assert.Equal(PaymentFixture.T0.AddMilliseconds(1), settlement.EffectiveAtUtc);
    }

    // Catches: the bank's reported debit time reaching the receipt with sub-millisecond ticks (BUG-008)
    [Fact]
    public async Task Bank_debit_time_is_cut_to_milliseconds()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.NextDebit(BankOutcomes.Completed(PaymentFixture.T0.AddSeconds(3).AddTicks(9999)));

        var receipt = await f.ConfirmOkAsync(quote.Reference);

        Assert.Equal(PaymentFixture.T0.AddSeconds(3), receipt.Time);
    }

    // T-U-10 · PR-04 · unit (application)
    // Given a £150.50 quote and the bank completing naturally, When ConfirmAsync, Then Ok receipt Completed, fee £0.00, Time = effective time, W-2 with £150.50 and £0.00, BalanceOwed £0.00, Repeated false; store balance £0.00; quote Confirmed; events ConfirmCreated, DebitRequested, BankCompleted in order; bank available £24,849.50 with one entry for the reference
    // Catches: a confirm that moves money on one side only, or a receipt that misstates the outcome (AC-7, R-9, R-12)
    [Fact]
    public async Task Confirm_completes_and_updates_both_sides()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var bankTime = PaymentFixture.T0.AddSeconds(3);
        f.Bank.NextDebit(BankOutcomes.Completed(bankTime));

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.True(result.IsOk);
        var receipt = result.Value;
        Assert.Equal(quote.Reference, receipt.Reference);
        Assert.Equal(PaymentState.Completed, receipt.State);
        Assert.Equal(TestMoney.Of("150.50"), receipt.Amount);
        Assert.Equal(Money.Zero, receipt.Fee);
        Assert.Equal(bankTime, receipt.Time);
        Assert.Equal("Brightline Supplies Ltd current account, ending 7731", receipt.FromLabel);
        Assert.Equal("Ops van card, ending 0001", receipt.ToLabel);
        Assert.Null(receipt.Reason);
        Assert.Equal("Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference PAY-00000001.", receipt.Message);
        Assert.Equal(Money.Zero, receipt.BalanceOwed);
        Assert.False(receipt.Repeated);
        Assert.Equal(PaymentOrigin.CardholderView, receipt.Origin);

        var balance = await f.Store.GetBalanceAsync(f.Card.Id, Ct);
        Assert.Equal(Money.Zero, balance!.Owed);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal(QuoteStatus.Confirmed, record.Status);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankCompleted], Kinds(record));
        var settlement = record.Payment!.Settlement!;
        Assert.Equal(PaymentFixture.T0, settlement.SettledAtUtc);
        Assert.Equal(bankTime, settlement.EffectiveAtUtc);
        Assert.Equal(bankTime, settlement.BankDebitedAtUtc);
        Assert.Equal(TestMoney.Of("150.50"), settlement.BalanceBefore);
        Assert.Equal(Money.Zero, settlement.BalanceAfter);
        Assert.Equal(TestMoney.Of("24849.50"), f.Bank.AvailableBalance);
        var entry = Assert.Single(f.Bank.Entries);
        Assert.Equal(quote.Reference, entry.Reference);
        var request = Assert.Single(f.Bank.DebitRequests);
        Assert.Equal("CardLab card payment PAY-00000001", request.Narrative);
        Assert.Equal(new BankAccountRef(Seed.BankAccountRef), request.Account);
        Assert.Equal(TestMoney.Of("150.50"), request.Amount);
    }

    // T-U-11 · PR-04 · unit (application)
    // Given a £150.50 quote and then a £20.00 approved purchase on the card, When ConfirmAsync, Then BalanceChanged with W-6 for £170.50 and BalanceOwed £170.50; quote Refused with reason balance_changed and BalanceAtRefusal £170.50; one ConfirmRefused event; no payment; the bank received no request
    // Catches: paying a different amount from the one reviewed (AC-8, R-5, OB-7)
    [Fact]
    public async Task Balance_change_refuses_and_records_the_attempt()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        await f.AddPurchaseAsync("20.00");

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        var error = Assert.IsType<PaymentError.BalanceChanged>(result.Error);
        Assert.Equal("Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.", error.Message);
        Assert.Equal(TestMoney.Of("170.50"), error.BalanceOwed);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal(QuoteStatus.Refused, record.Status);
        Assert.Equal("balance_changed", record.RefusalReason);
        Assert.Equal(TestMoney.Of("170.50"), record.BalanceAtRefusal);
        Assert.Equal([PaymentEventKind.ConfirmRefused], Kinds(record));
        Assert.Equal("balance_changed", record.Events[0].Detail);
        Assert.Null(record.Payment);
        Assert.Equal(0, f.Store.PaymentCount);
        Assert.Empty(f.Bank.DebitRequests);
    }

    // T-U-19 · PR-04 · unit (application)
    // Given a reference refused for balance_changed, When ConfirmAsync again, Then QuoteRefused with the same W-6 text for the current balance, a second ConfirmRefused event, still no payment; and a new QuoteAsync returns a different reference
    // Catches: a refused reference later starting a payment (AC-30, R-7)
    [Fact]
    public async Task Refused_reference_stays_refused()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        await f.AddPurchaseAsync("20.00");
        await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        var again = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(["refused_balance_changed", "refused_already_refused"], f.Logger.Events("PaymentConfirmAttempt").Select(e => (string)e.Fields["Result"]!).ToArray());
        var error = Assert.IsType<PaymentError.QuoteRefused>(again.Error);
        Assert.Equal("Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.", error.Message);
        Assert.Equal(TestMoney.Of("170.50"), error.BalanceOwed);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmRefused, PaymentEventKind.ConfirmRefused], Kinds(record));
        Assert.Equal(0, f.Store.PaymentCount);
        Assert.Empty(f.Bank.DebitRequests);

        var fresh = await f.QuoteOkAsync();
        Assert.Equal(new PaymentReference("PAY-00000002"), fresh.Reference);
        Assert.Equal(TestMoney.Of("170.50"), fresh.Amount);
    }

    // T-U-12 · PR-04 · unit (application)
    // Given a completed confirm, When ConfirmAsync twice more for the same reference, Then each returns the original receipt with Repeated true; one payment; one DebitRequest at the bank; two ConfirmRepeated events with their times
    // Catches: double click or retry creating a second payment or debit (AC-9, R-7)
    [Fact]
    public async Task Repeated_confirm_returns_the_original_receipt()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var original = await f.ConfirmOkAsync(quote.Reference);

        f.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        var third = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(original with { Repeated = true }, second.Value);
        Assert.Equal(original with { Repeated = true }, third.Value);
        Assert.Equal(1, f.Store.PaymentCount);
        Assert.Single(f.Bank.DebitRequests);
        var record = await f.RecordAsync(quote.Reference);
        var repeats = record.Events.Where(e => e.Kind == PaymentEventKind.ConfirmRepeated).ToArray();
        Assert.Equal([PaymentFixture.T0.AddMinutes(1), PaymentFixture.T0.AddMinutes(2)], repeats.Select(e => e.AtUtc).ToArray());
    }

    // T-U-13 · PR-04 · unit (application)
    // Given the bank holding debits until a gate, When two ConfirmAsync for one reference run concurrently and the gate is then released, Then both receipts are Completed with the same reference, amount and time (one Repeated), one payment and one DebitRequest
    // Catches: concurrent confirms racing into two payments or two different answers (AC-10, DEC-5)
    [Fact]
    public async Task Concurrent_confirms_share_one_payment()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var gate = new TaskCompletionSource();
        f.Bank.HoldDebitsUntil(gate.Task);

        var a = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        var b = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        Assert.False(a.IsCompleted);
        Assert.False(b.IsCompleted);
        gate.SetResult();
        var results = await Task.WhenAll(a, b);

        var receipts = results.Select(r => r.Value!).ToArray();
        Assert.All(receipts, r => Assert.Equal(PaymentState.Completed, r.State));
        Assert.Equal(receipts[0].Reference, receipts[1].Reference);
        Assert.Equal(receipts[0].Amount, receipts[1].Amount);
        Assert.Equal(receipts[0].Time, receipts[1].Time);
        Assert.Equal(1, receipts.Count(r => r.Repeated));
        Assert.Equal(1, f.Store.PaymentCount);
        Assert.Single(f.Bank.DebitRequests);
    }

    // T-U-14 · PR-04 · unit (application)
    // Given a pending payment PAY-00000001 of £150.50 (bank Hang) and an open quote PAY-00000002 minted before it, When QuoteAsync and ConfirmAsync(PAY-00000002), Then both fail PaymentPending with W-9 naming £150.50 and PAY-00000001; nothing new is stored
    // Catches: a second payment in flight on one card (AC-11, R-8)
    [Fact]
    public async Task Pending_payment_blocks_new_quotes_and_confirms()
    {
        var f = await PaymentFixture.CreateAsync(bankTimeoutSeconds: 0);
        var first = await f.QuoteOkAsync();
        var second = await f.QuoteOkAsync();
        f.Bank.Hang();
        await f.ConfirmOkAsync(first.Reference);
        const string w9 = "A payment of £150.50 (reference PAY-00000001) is still pending. You cannot make another payment until we know its outcome. Choose Check status.";

        var quote = await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct);
        var confirm = await f.Service.ConfirmAsync(f.Card.Id, second.Reference, Ct);

        var quoteError = Assert.IsType<PaymentError.PaymentPending>(quote.Error);
        Assert.Equal(w9, quoteError.Message);
        Assert.Equal(first.Reference, quoteError.PendingReference);
        Assert.Equal(TestMoney.Of("150.50"), quoteError.Amount);
        var confirmError = Assert.IsType<PaymentError.PaymentPending>(confirm.Error);
        Assert.Equal(w9, confirmError.Message);
        Assert.Equal(first.Reference, confirmError.PendingReference);
        Assert.Equal(2, f.Store.QuoteCount);
        Assert.Equal(1, f.Store.PaymentCount);
        Assert.Single(f.Bank.DebitRequests);
        var record = await f.RecordAsync(second.Reference);
        Assert.Equal([PaymentEventKind.ConfirmRefused], Kinds(record));
        Assert.Equal("payment_pending", record.Events[0].Detail);
        Assert.Equal(QuoteStatus.Open, record.Status);
    }

    // T-U-15 · PR-04 · unit (application)
    // Given a £150.50 quote and NextDebit(InsufficientFunds), When ConfirmAsync, Then receipt Failed, Reason InsufficientFunds, fee £0.00, W-3 with £150.50; balance £150.50; bank balance and entries unchanged; events ConfirmCreated, DebitRequested, BankRefused; a new quote is allowed
    // Catches: a refused debit reducing the balance or blocking later payments (AC-12, R-10)
    [Fact]
    public async Task Insufficient_funds_fails_and_nothing_moves()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.NextDebit(BankOutcomes.InsufficientFunds());

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Failed, receipt.State);
        Assert.Equal(FailureReason.InsufficientFunds, receipt.Reason);
        Assert.Equal(Money.Zero, receipt.Fee);
        Assert.Equal("Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still £150.50. Add money to the account, then try again. Reference PAY-00000001.", receipt.Message);
        Assert.Equal(TestMoney.Of("150.50"), receipt.BalanceOwed);
        var balance = await f.Store.GetBalanceAsync(f.Card.Id, Ct);
        Assert.Equal(TestMoney.Of("150.50"), balance!.Owed);
        Assert.Equal(TestMoney.Of("25000.00"), f.Bank.AvailableBalance);
        Assert.Empty(f.Bank.Entries);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankRefused], Kinds(record));
        Assert.Equal(TestMoney.Of("150.50"), record.Payment!.Settlement!.BalanceAfter);
        Assert.True((await f.Service.QuoteAsync(f.Card.Id, PaymentOrigin.CardholderView, Ct)).IsOk);
    }

    // T-U-20 · PR-04 · unit (application)
    // Given a £150.50 quote and NextDebit(AccountUnavailable), When ConfirmAsync, Then Failed with AccountUnavailable, fee £0.00 and W-4 with £150.50; nothing moved
    // Catches: the wrong refusal text (AC-35, R-10)
    [Fact]
    public async Task Account_unavailable_fails_with_W4()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.NextDebit(BankOutcomes.AccountUnavailable());

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Failed, receipt.State);
        Assert.Equal(FailureReason.AccountUnavailable, receipt.Reason);
        Assert.Equal(Money.Zero, receipt.Fee);
        Assert.Equal(PaymentFixture.W4, receipt.Message);
        Assert.Equal(TestMoney.Of("25000.00"), f.Bank.AvailableBalance);
        var balance = await f.Store.GetBalanceAsync(f.Card.Id, Ct);
        Assert.Equal(TestMoney.Of("150.50"), balance!.Owed);
    }

    // T-U-26 · PR-04 · unit (application)
    // Given a £150.50 quote and bank.Hang(), When ConfirmAsync, Then receipt Pending with W-5 (balance £150.50) and Time = acceptance time; events ConfirmCreated, DebitRequested, BankUnanswered; one BankCallUnanswered log with call "debit"; PaymentStateChanged only for none → pending
    // Catches: an unanswered debit treated as failed (money may have moved) or retried (AC-13, R-11)
    [Fact]
    public async Task Unanswered_debit_leaves_the_payment_pending()
    {
        var f = await PaymentFixture.CreateAsync(bankTimeoutSeconds: 0);
        var quote = await f.QuoteOkAsync();
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        f.Bank.Hang();

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        var receipt = result.Value!;
        Assert.Equal(PaymentState.Pending, receipt.State);
        Assert.Equal(PaymentFixture.W5, receipt.Message);
        Assert.Equal(TestMoney.Of("150.50"), receipt.BalanceOwed);
        Assert.Equal(Money.Zero, receipt.Fee);
        Assert.Equal(PaymentFixture.T0.AddMinutes(1), receipt.Time);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankUnanswered], Kinds(record));
        Assert.Equal(PaymentState.Pending, record.Payment!.State);
        Assert.Null(record.Payment.Settlement);
        var unanswered = Assert.Single(f.Logger.Events("BankCallUnanswered"));
        Assert.Equal("debit", unanswered.Fields["Call"]);
        Assert.Equal("PAY-00000001", unanswered.Fields["Reference"]);
        var changed = Assert.Single(f.Logger.Events("PaymentStateChanged"));
        Assert.Equal("none", changed.Fields["FromState"]);
        Assert.Equal("pending", changed.Fields["ToState"]);
        Assert.Single(f.Bank.DebitRequests);
    }

    // Catches: the budget not reaching the bank (a call that never answers would hang the confirm) or a plain timeout leaking exception jargon into the record (NFR-2, AC-13, design §9)
    [Fact(Timeout = 5000)]
    public async Task Bank_call_is_bounded_by_the_budget()
    {
        var f = await PaymentFixture.CreateAsync(bankTimeoutSeconds: 0);
        var quote = await f.QuoteOkAsync();
        f.Bank.HoldDebitsUntil(new TaskCompletionSource().Task);

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(PaymentState.Pending, result.Value!.State);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal(PaymentEventKind.BankUnanswered, record.Events[^1].Kind);
        Assert.Null(record.Events[^1].Detail);
        Assert.False(record.Events[^1].Simulated, "a real budget expiry is not a simulated outcome");
    }

    // Catches: a cancellation that is not the budget firing (e.g. an adapter abort) treated as "no answer" instead of an adapter failure (design §9)
    [Fact]
    public async Task Cancellation_outside_the_budget_is_an_adapter_failure()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.FailNext(new OperationCanceledException("adapter aborted"));

        await f.ConfirmOkAsync(quote.Reference);

        var last = (await f.RecordAsync(quote.Reference)).Events[^1];
        Assert.Equal(PaymentEventKind.BankUnanswered, last.Kind);
        Assert.Equal("OperationCanceledException", last.Detail);
        Assert.False(last.Simulated);
    }

    // Catches: a steered no-answer recorded as a real bank non-answer (BUG-010, R-15, AC-33)
    [Fact]
    public async Task Steered_no_answer_is_recorded_as_simulated()
    {
        var f = await PaymentFixture.CreateAsync(bankTimeoutSeconds: 0);
        var quote = await f.QuoteOkAsync();
        f.Bank.Hang();

        await f.ConfirmOkAsync(quote.Reference);

        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal(PaymentEventKind.BankUnanswered, record.Events[^1].Kind);
        Assert.True(record.Events[^1].Simulated);
        Assert.Null(record.Events[^1].Detail);
    }

    // Catches: a client disconnect cancelling the debit or leaving the payment half-applied (BUG-002, NFR-2)
    [Fact(Timeout = 5000)]
    public async Task Client_disconnect_never_cancels_the_debit_and_the_payment_settles()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        var gate = new TaskCompletionSource();
        f.Bank.HoldDebitsUntil(gate.Task);
        using var cts = new CancellationTokenSource();

        var confirm = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, cts.Token);
        await cts.CancelAsync();
        gate.SetResult();
        var result = await confirm;

        Assert.Equal(PaymentState.Completed, result.Value!.State);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankCompleted], Kinds(record));
        Assert.Empty(f.Logger.Events("BankCallUnanswered"));
        Assert.False(f.InFlight.TryGet(quote.Reference, out _));
        Assert.Single(f.Bank.Entries);
    }

    // Catches: a joined repeat confirm failing with the first caller's fault instead of answering from the stored state (AC-9, AC-10)
    [Fact(Timeout = 5000)]
    public async Task Joined_repeat_survives_the_first_callers_failure()
    {
        var faults = new LockFaults();
        var f = await PaymentFixture.CreateAsync(decorate: store => new LockFaultingStore(store, faults));
        var quote = await f.QuoteOkAsync();
        var gate = new TaskCompletionSource();
        f.Bank.HoldDebitsUntil(gate.Task);
        faults.Error = () => new InvalidOperationException("database unavailable");
        faults.FailCalls(3);

        var first = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        var second = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        gate.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        var joined = (await second).Value!;
        Assert.Equal(PaymentState.Pending, joined.State);
        Assert.True(joined.Repeated);
        Assert.Equal(PaymentFixture.W5, joined.Message);
    }

    // Catches: the in-flight task registered after lock #1 commits, so a confirm entering in between misses it and answers Pending (DEC-5, AC-10)
    [Fact(Timeout = 5000)]
    public async Task In_flight_task_is_registered_before_the_first_confirm_commits()
    {
        var gated = new GatedStore();
        var f = await PaymentFixture.CreateAsync(decorate: store => gated.Wrap(store));
        var quote = await f.QuoteOkAsync();
        var committed = new TaskCompletionSource();
        var resume = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        f.Bank.HoldDebitsUntil(gate.Task);
        gated.PauseAfter(1, committed, resume.Task);

        var first = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        await committed.Task;
        var second = f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);
        resume.SetResult();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.Equal(PaymentState.Completed, r.Value!.State));
        Assert.Equal(1, f.Store.PaymentCount);
        Assert.Single(f.Bank.DebitRequests);
    }

    // T-U-27 · PR-04 · unit (application)
    // Given bank.FailNext(new InvalidOperationException()), When ConfirmAsync, Then the payment stays Pending with W-5 and a BankUnanswered event whose Detail is "InvalidOperationException"; no exception escapes
    // Catches: an adapter error failing a payment the bank may have applied (design §9)
    [Fact]
    public async Task Adapter_error_is_treated_as_unknown()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.FailNext(new InvalidOperationException());

        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(PaymentState.Pending, result.Value!.State);
        Assert.Equal(PaymentFixture.W5, result.Value.Message);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal(PaymentEventKind.BankUnanswered, record.Events[^1].Kind);
        Assert.Equal("InvalidOperationException", record.Events[^1].Detail);
    }

    // T-U-28 · PR-04 · unit (application)
    // Given a completed confirm, a repeat and a balance-changed refusal, When the logger is read, Then exactly two PaymentStateChanged (none→pending, pending→completed) with PaymentId, CardId, LastFour 0001, Kind full, Amount 150.50, FromState, ToState, Reason "-" and DurationMs; PaymentConfirmAttempt results created, repeated, refused_balance_changed; no field contains a 16-digit run
    // Catches: missing, duplicated or number-leaking log events (AC-27, NFR-4, NFR-3)
    [Fact]
    public async Task Logs_one_state_change_event_per_change()
    {
        var f = await PaymentFixture.CreateAsync();
        var first = await f.QuoteOkAsync();
        var second = await f.QuoteOkAsync();
        await f.ConfirmOkAsync(first.Reference);
        await f.Service.ConfirmAsync(f.Card.Id, first.Reference, Ct);
        await f.Service.ConfirmAsync(f.Card.Id, second.Reference, Ct);

        var changes = f.Logger.Events("PaymentStateChanged");
        Assert.Equal(2, changes.Count);
        Assert.Equal(["none", "pending"], new[] { changes[0].Fields["FromState"], changes[0].Fields["ToState"] });
        Assert.Equal(["pending", "completed"], new[] { changes[1].Fields["FromState"], changes[1].Fields["ToState"] });
        Assert.All(changes, c =>
        {
            Assert.Equal(f.Card.Id.Value.ToString(), c.Fields["CardId"]);
            Assert.Equal("0001", c.Fields["LastFour"]);
            Assert.Equal("full", c.Fields["Kind"]);
            Assert.Equal("150.50", c.Fields["Amount"]);
            Assert.Equal("-", c.Fields["Reason"]);
            Assert.True(c.Fields["DurationMs"] is long ms && ms >= 0);
            Assert.False(string.IsNullOrEmpty((string?)c.Fields["PaymentId"]));
        });
        Assert.Equal(changes[0].Fields["PaymentId"], changes[1].Fields["PaymentId"]);
        var attempts = f.Logger.Events("PaymentConfirmAttempt").Select(e => (string)e.Fields["Result"]!).ToArray();
        Assert.Equal(["created", "repeated", "refused_balance_changed"], attempts);
        var digitRun = new Regex(@"\d{16}");
        Assert.All(f.Logger.Entries, e =>
        {
            Assert.DoesNotMatch(digitRun, e.Message);
            Assert.All(e.Fields.Values, v => Assert.DoesNotMatch(digitRun, v?.ToString() ?? string.Empty));
        });
    }

    [Fact]
    public async Task Failed_pending_and_blocked_attempts_log_their_results()
    {
        var f = await PaymentFixture.CreateAsync(bankTimeoutSeconds: 0);
        var first = await f.QuoteOkAsync();
        var second = await f.QuoteOkAsync();
        var third = await f.QuoteOkAsync();
        f.Bank.NextDebit(BankOutcomes.InsufficientFunds());
        await f.ConfirmOkAsync(first.Reference);
        f.Bank.Hang();
        await f.ConfirmOkAsync(second.Reference);
        await f.Service.ConfirmAsync(f.Card.Id, third.Reference, Ct);

        var attempts = f.Logger.Events("PaymentConfirmAttempt").Select(e => (string)e.Fields["Result"]!).ToArray();
        Assert.Equal(["created", "created", "refused_payment_pending"], attempts);
        var changes = f.Logger.Events("PaymentStateChanged");
        Assert.Equal(3, changes.Count);
        Assert.Equal("failed", changes[1].Fields["ToState"]);
        Assert.Equal("insufficient_funds", changes[1].Fields["Reason"]);
    }

    // T-U-31 · PR-04 · unit (application)
    // Given a store decorator whose settle lock (second and third WithCardLockAsync of a confirm) throws CardLockTimeoutException, options SettleLockRetryMs 0, When ConfirmAsync with a completing bank, Then the receipt is Pending (W-5) and the best-effort fourth lock appended a BankUnanswered event with Detail "settle_lock_timeout"; when it throws only once, the retry settles Completed
    // Catches: a lock timeout after the bank answered losing the outcome or failing the payment (design §9)
    [Fact]
    public async Task Settle_lock_timeout_retries_once_then_stays_pending()
    {
        var faults = new LockFaults();
        var f = await PaymentFixture.CreateAsync(decorate: store => new LockFaultingStore(store, faults));
        var quote = await f.QuoteOkAsync();

        faults.FailCalls(2, 3);
        var stuck = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(PaymentState.Pending, stuck.Value!.State);
        Assert.Equal(PaymentFixture.W5, stuck.Value.Message);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankUnanswered], Kinds(record));
        Assert.Equal("settle_lock_timeout", record.Events[^1].Detail);
        Assert.Single(f.Bank.DebitRequests);
        Assert.Empty(f.Logger.Events("BankCallUnanswered"));

        var healed = await f.Service.CheckStatusAsync(f.Card.Id, quote.Reference, Ct);
        Assert.Equal(PaymentState.Completed, healed.Value!.State);
        Assert.Single(f.Bank.DebitRequests);
        Assert.Single(f.Bank.Entries);
    }

    [Fact]
    public async Task Settle_lock_timeout_with_no_best_effort_lock_still_returns_pending()
    {
        var faults = new LockFaults();
        var f = await PaymentFixture.CreateAsync(decorate: store => new LockFaultingStore(store, faults));
        var quote = await f.QuoteOkAsync();

        faults.FailCalls(2, 3, 4);
        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(PaymentState.Pending, result.Value!.State);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested], Kinds(record));
    }

    [Fact]
    public async Task One_settle_lock_timeout_is_retried_and_settles()
    {
        var faults = new LockFaults();
        var f = await PaymentFixture.CreateAsync(decorate: store => new LockFaultingStore(store, faults));
        var quote = await f.QuoteOkAsync();

        faults.FailCalls(2);
        var result = await f.Service.ConfirmAsync(f.Card.Id, quote.Reference, Ct);

        Assert.Equal(PaymentState.Completed, result.Value!.State);
        var record = await f.RecordAsync(quote.Reference);
        Assert.Equal([PaymentEventKind.ConfirmCreated, PaymentEventKind.DebitRequested, PaymentEventKind.BankCompleted], Kinds(record));
    }

    // T-U-38 · PR-04 · unit (application)
    // Given a completed payment, When GetReceiptAsync, Then the stored receipt (Repeated false) and no bank call; another card's reference or an open quote without a payment → ReferenceNotFound; unknown card → CardNotFound
    // Catches: receipt reload calling the bank or exposing another card's payment (design §7)
    [Fact]
    public async Task Receipt_reload_reads_without_calling_the_bank()
    {
        var f = await PaymentFixture.CreateAsync();
        var paid = await f.QuoteOkAsync();
        var original = await f.ConfirmOkAsync(paid.Reference);

        var reload = await f.Service.GetReceiptAsync(f.Card.Id, paid.Reference, Ct);

        Assert.Equal(original, reload.Value);
        await f.AddPurchaseAsync("10.00");
        var open = await f.QuoteOkAsync();
        var later = await f.Service.GetReceiptAsync(f.Card.Id, paid.Reference, Ct);
        Assert.Equal(TestMoney.Of("10.00"), later.Value!.BalanceOwed);
        Assert.Single(f.Bank.DebitRequests);
        Assert.Equal(0, f.Bank.StatusChecks);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.GetReceiptAsync(f.Other.Id, paid.Reference, Ct)).Error);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.GetReceiptAsync(f.Card.Id, open.Reference, Ct)).Error);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.GetReceiptAsync(f.Card.Id, new PaymentReference("PAY-0000ZZZZ"), Ct)).Error);
        Assert.IsType<PaymentError.CardNotFound>((await f.Service.GetReceiptAsync(new CardId(Guid.NewGuid()), paid.Reference, Ct)).Error);
    }

    [Fact]
    public async Task Confirming_another_cards_reference_or_an_unknown_one_is_not_found()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();

        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.ConfirmAsync(f.Other.Id, quote.Reference, Ct)).Error);
        Assert.IsType<PaymentError.ReferenceNotFound>((await f.Service.ConfirmAsync(f.Card.Id, new PaymentReference("PAY-0000ZZZZ"), Ct)).Error);
        Assert.IsType<PaymentError.CardNotFound>((await f.Service.ConfirmAsync(new CardId(Guid.NewGuid()), quote.Reference, Ct)).Error);
        Assert.Equal(0, f.Store.PaymentCount);
        Assert.Empty(f.Bank.DebitRequests);
    }

    internal sealed class LockFaults
    {
        private readonly object _gate = new();
        private HashSet<int> _failing = [];
        private int _calls;

        public Func<Exception> Error { get; set; } = () => new CardLockTimeoutException("scripted");

        public void FailCalls(params int[] calls)
        {
            lock (_gate)
            {
                _failing = [.. calls];
                _calls = 0;
            }
        }

        public bool ShouldFail()
        {
            lock (_gate)
            {
                _calls++;
                return _failing.Contains(_calls);
            }
        }
    }

    internal sealed class LockFaultingStore(ICardStore inner, LockFaults faults) : ICardStore
    {
        public Task<IReadOnlyList<CardLab.Domain.Cards.CardSummary>> ListCardsAsync(CancellationToken ct = default) => inner.ListCardsAsync(ct);

        public Task<CardLab.Domain.Cards.CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default) => inner.GetCardAsync(id, ct);

        public Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default) => inner.FindByLookupAsync(hash, ct);

        public Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default) =>
            faults.ShouldFail()
                ? throw faults.Error()
                : inner.WithCardLockAsync(id, work, ct);

        public Task<IReadOnlyList<CardLab.Domain.Cards.TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default) => inner.ListTransactionsAsync(id, ct);

        public Task<IReadOnlyList<CardLab.Domain.Cards.AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default) => inner.ListAuditAsync(id, ct);

        public Task<LinkedAccount?> GetLinkedAccountAsync(CancellationToken ct = default) => inner.GetLinkedAccountAsync(ct);

        public Task<CardBalance?> GetBalanceAsync(CardId id, CancellationToken ct = default) => inner.GetBalanceAsync(id, ct);

        public Task<PaymentRecord?> GetPaymentRecordAsync(PaymentReference reference, CancellationToken ct = default) => inner.GetPaymentRecordAsync(reference, ct);

        public Task<IReadOnlyList<PaymentRecord>> ListPaymentRecordsAsync(CardId id, CancellationToken ct = default) => inner.ListPaymentRecordsAsync(id, ct);

        public Task<IReadOnlyList<ActivityEntry>> ListActivityAsync(CardId id, int take, CancellationToken ct = default) => inner.ListActivityAsync(id, take, ct);
    }
}

/// <summary>Store decorator that can pause a unit of work before it starts or after it has committed (counted from arming).</summary>
internal sealed class GatedStore
{
    private readonly object _gate = new();
    private int _calls;
    private int _pauseBefore;
    private Task? _beforeGate;
    private int _pauseAfter;
    private TaskCompletionSource? _committed;
    private Task? _afterGate;

    public ICardStore Wrap(ICardStore inner) => new Store(this, inner);

    public void PauseBefore(int call, Task gate)
    {
        lock (_gate)
        {
            (_calls, _pauseBefore, _beforeGate) = (0, call, gate);
        }
    }

    public void PauseAfter(int call, TaskCompletionSource committed, Task resume)
    {
        lock (_gate)
        {
            (_calls, _pauseAfter, _committed, _afterGate) = (0, call, committed, resume);
        }
    }

    private sealed class Store(GatedStore owner, ICardStore inner) : ICardStore
    {
        public Task<IReadOnlyList<CardLab.Domain.Cards.CardSummary>> ListCardsAsync(CancellationToken ct = default) => inner.ListCardsAsync(ct);

        public Task<CardLab.Domain.Cards.CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default) => inner.GetCardAsync(id, ct);

        public Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default) => inner.FindByLookupAsync(hash, ct);

        public async Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default)
        {
            int call;
            Task? before;
            lock (owner._gate)
            {
                call = ++owner._calls;
                before = call == owner._pauseBefore ? owner._beforeGate : null;
            }

            if (before is not null)
            {
                await before;
            }

            var result = await inner.WithCardLockAsync(id, work, ct);
            if (call == owner._pauseAfter)
            {
                owner._committed!.SetResult();
                await owner._afterGate!;
            }

            return result;
        }

        public Task<IReadOnlyList<CardLab.Domain.Cards.TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default) => inner.ListTransactionsAsync(id, ct);

        public Task<IReadOnlyList<CardLab.Domain.Cards.AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default) => inner.ListAuditAsync(id, ct);

        public Task<LinkedAccount?> GetLinkedAccountAsync(CancellationToken ct = default) => inner.GetLinkedAccountAsync(ct);

        public Task<CardBalance?> GetBalanceAsync(CardId id, CancellationToken ct = default) => inner.GetBalanceAsync(id, ct);

        public Task<PaymentRecord?> GetPaymentRecordAsync(PaymentReference reference, CancellationToken ct = default) => inner.GetPaymentRecordAsync(reference, ct);

        public Task<IReadOnlyList<PaymentRecord>> ListPaymentRecordsAsync(CardId id, CancellationToken ct = default) => inner.ListPaymentRecordsAsync(id, ct);

        public Task<IReadOnlyList<ActivityEntry>> ListActivityAsync(CardId id, int take, CancellationToken ct = default) => inner.ListActivityAsync(id, take, ct);
    }
}

/// <summary>Shared arrangement for the payment application tests: Ops van, Field sales, the seeded account and bank, all fakes.</summary>
internal sealed class PaymentFixture
{
    public const string W4 = "Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference PAY-00000001.";

    public const string W5 = "Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference PAY-00000001.";

    public const string W10 = "Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference PAY-00000001.";

    public static readonly DateTimeOffset T0 = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

    private PaymentFixture(Func<InMemoryCardStore, ICardStore>? decorate, string bankOpening, int bankTimeoutSeconds)
    {
        Card = CardBuilder.From(Seed.OpsVan).Build();
        Other = CardBuilder.From(Seed.FieldSales).Build();
        Clock = new FakeClock(T0);
        Store = new InMemoryCardStore(Card, Other).WithLinkedAccount(new LinkedAccountBuilder().Build());
        Bank = new FakeBank(Clock, new BankAccountRef(Seed.BankAccountRef), Seed.LinkedAccountLastFour, TestMoney.Of(bankOpening));
        References = new FakePaymentReferences();
        InFlight = new InFlightPayments();
        Logger = new CapturingLogger<PaymentService>();
        Service = new PaymentService(
            decorate is null ? Store : decorate(Store),
            Bank,
            References,
            Clock,
            InFlight,
            new PaymentOptions { BankTimeoutSeconds = bankTimeoutSeconds, SettleLockRetryMs = 0 },
            Logger);
    }

    public StoreSeedCard Card { get; }

    public StoreSeedCard Other { get; }

    public FakeClock Clock { get; }

    public InMemoryCardStore Store { get; }

    public FakeBank Bank { get; }

    public FakePaymentReferences References { get; }

    public InFlightPayments InFlight { get; }

    public CapturingLogger<PaymentService> Logger { get; }

    public PaymentService Service { get; }

    public static async Task<PaymentFixture> CreateAsync(
        string? purchases = "150.50",
        Func<InMemoryCardStore, ICardStore>? decorate = null,
        string bankOpening = Seed.BankOpeningBalance,
        int bankTimeoutSeconds = 10)
    {
        var fixture = new PaymentFixture(decorate, bankOpening, bankTimeoutSeconds);
        if (purchases is not null)
        {
            await fixture.AddPurchaseAsync(purchases);
        }

        return fixture;
    }

    public Task AddPurchaseAsync(string amount) => AddTransactionAsync(new TransactionBuilder().Amount(amount).Approved().Build());

    public Task AddTransactionAsync(NewTransaction transaction) =>
        Store.WithCardLockAsync(
            Card.Id,
            w =>
            {
                w.AppendTransaction(transaction);
                return Task.FromResult(true);
            },
            TestContext.Current.CancellationToken);

    public async Task<Quote> QuoteOkAsync()
    {
        var result = await Service.QuoteAsync(Card.Id, PaymentOrigin.CardholderView, TestContext.Current.CancellationToken);
        return result.IsOk ? result.Value : throw new InvalidOperationException("Quote refused: " + result.Error);
    }

    public async Task<Receipt> ConfirmOkAsync(PaymentReference reference)
    {
        var result = await Service.ConfirmAsync(Card.Id, reference, TestContext.Current.CancellationToken);
        return result.IsOk ? result.Value : throw new InvalidOperationException("Confirm refused: " + result.Error);
    }

    public async Task<PaymentRecord> RecordAsync(PaymentReference reference) =>
        await Store.GetPaymentRecordAsync(reference, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("No record for " + reference);
}
