using CardLab.Domain.Application;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Application;

public sealed class CardholderViewServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CardholderViewService ViewOf(PaymentFixture f) => new(f.Store, f.Bank);

    // T-U-29 · PR-04 · unit (application)
    // Given Ops van with approved £120.00 and £30.50, a declined £500.00, the seeded linked account and a FakeBank at £25,000.00 with 12 entries, When GetAsync, Then card name, last four 0001, cardholder Priya Nair, balance £150.50, Available with no message, account name / 7731 / £25,000.00 / authority, at most 10 card rows newest first and 10 account entries; at £0.00 → NothingToPay with W-7; at £1,000,000.01 → AboveLimit with W-8
    // Catches: a view that misstates the balance or offers pay when it must not (AC-1, AC-4, AC-5, AC-24, R-16)
    [Fact]
    public async Task View_shows_balance_account_and_availability()
    {
        var f = await PaymentFixture.CreateAsync(purchases: null, bankOpening: "25012.00");
        await f.AddPurchaseAsync("120.00");
        await f.AddPurchaseAsync("30.50");
        var day = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        await f.AddTransactionAsync(new TransactionBuilder().Amount("500.00").At(day.AddDays(3)).Declined(Reason.LimitExceeded).Build());
        for (var i = 4; i <= 13; i++)
        {
            await f.AddTransactionAsync(new TransactionBuilder().Amount("1.00").At(day.AddDays(i)).Declined(Reason.LimitExceeded).Build());
        }

        for (var i = 1; i <= 12; i++)
        {
            await f.Bank.RequestDebitAsync(new DebitRequest(f.Bank.Account, new PaymentReference($"PAY-{i:D8}"), TestMoney.Of("1.00"), "seed"), Ct);
        }

        var result = await ViewOf(f).GetAsync(f.Card.Id, Ct);

        var view = result.Value!;
        Assert.Equal(f.Card.Id, view.Card.Id);
        Assert.Equal("0001", view.Card.LastFour);
        Assert.Equal("Ops van", view.CardName);
        Assert.Equal("Priya Nair", view.CardholderName);
        Assert.Equal(CardStatus.Active, view.Status);
        Assert.Equal(TestMoney.Of("150.50"), view.BalanceOwed);
        Assert.Equal(PayAvailability.Available, view.Availability);
        Assert.Null(view.AvailabilityMessage);
        Assert.Null(view.PendingPayment);
        Assert.Equal("Brightline Supplies Ltd current account", view.Account.DisplayName);
        Assert.Equal("7731", view.Account.LastFour);
        Assert.Equal(Seed.AuthorityBasis, view.Account.AuthorityBasis);
        Assert.Equal(TestMoney.Of("25000.00"), view.AccountAvailable);
        Assert.Equal(10, view.CardActivity.Count);
        Assert.Equal(day.AddDays(13), view.CardActivity[0].At);
        Assert.Equal(10, view.AccountActivity.Count);
        Assert.Equal("PAY-00000012", view.AccountActivity[0].Reference.Value);
        Assert.Equal(0, f.Bank.StatusChecks);
    }

    [Fact]
    public async Task View_offers_nothing_to_pay_at_zero_balance()
    {
        var f = await PaymentFixture.CreateAsync(purchases: null);

        var view = (await ViewOf(f).GetAsync(f.Card.Id, Ct)).Value!;

        Assert.Equal(Money.Zero, view.BalanceOwed);
        Assert.Equal(PayAvailability.NothingToPay, view.Availability);
        Assert.Equal("Nothing to pay. Your card balance is £0.00.", view.AvailabilityMessage);
    }

    [Fact]
    public async Task View_refuses_pay_above_the_limit()
    {
        var f = await PaymentFixture.CreateAsync(purchases: "1000000.01");

        var view = (await ViewOf(f).GetAsync(f.Card.Id, Ct)).Value!;

        Assert.Equal(PayAvailability.AboveLimit, view.Availability);
        Assert.StartsWith("This balance cannot be paid here. Payments above £1,000,000.00 are not available online.", view.AvailabilityMessage);
        Assert.Contains("£1,000,000.01", view.AvailabilityMessage);
    }

    // T-U-40 · PR-04 · unit (application)
    // Given a pending £150.50 payment PAY-00000001, When GetAsync, Then availability PaymentPending with W-9 naming £150.50 and PAY-00000001, PendingPayment receipt with W-5, and the bank received no status check (the view is a pure read, DEC-13)
    // Catches: the pending payment hidden or the view writing (AC-11, AC-13)
    [Fact]
    public async Task View_shows_the_pending_payment_without_checking()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        f.Bank.Hang();
        await f.ConfirmOkAsync(quote.Reference);
        var eventsBefore = (await f.RecordAsync(quote.Reference)).Events.Count;

        var view = (await ViewOf(f).GetAsync(f.Card.Id, Ct)).Value!;

        Assert.Equal(PayAvailability.PaymentPending, view.Availability);
        Assert.Equal("A payment of £150.50 (reference PAY-00000001) is still pending. You cannot make another payment until we know its outcome. Choose Check status.", view.AvailabilityMessage);
        var pending = view.PendingPayment!;
        Assert.Equal(PaymentState.Pending, pending.State);
        Assert.Equal(PaymentFixture.W5, pending.Message);
        Assert.Equal(quote.Reference, pending.Reference);
        Assert.Equal(TestMoney.Of("150.50"), view.BalanceOwed);
        Assert.Equal(0, f.Bank.StatusChecks);
        Assert.Equal(eventsBefore, (await f.RecordAsync(quote.Reference)).Events.Count);
    }

    [Fact]
    public async Task View_after_a_completed_payment_has_no_pending_payment()
    {
        var f = await PaymentFixture.CreateAsync();
        var quote = await f.QuoteOkAsync();
        await f.ConfirmOkAsync(quote.Reference);

        var view = (await ViewOf(f).GetAsync(f.Card.Id, Ct)).Value!;

        Assert.Null(view.PendingPayment);
        Assert.Equal(PayAvailability.NothingToPay, view.Availability);
        Assert.Equal(TestMoney.Of("24849.50"), view.AccountAvailable);
        Assert.Equal(quote.Reference, Assert.Single(view.AccountActivity).Reference);
        Assert.Contains(view.CardActivity, a => a.Kind == ActivityKind.Payment && a.PaymentState == PaymentState.Completed);
    }

    // T-U-41 · PR-04 · unit (application)
    // Given no card with the id, When GetAsync, Then CardNotFound
    // Catches: exceptions for unknown cards (API 404)
    [Fact]
    public async Task Unknown_card_is_not_found()
    {
        var f = await PaymentFixture.CreateAsync();

        var result = await ViewOf(f).GetAsync(new CardId(Guid.NewGuid()), Ct);

        Assert.IsType<PaymentError.CardNotFound>(result.Error);
    }
}
