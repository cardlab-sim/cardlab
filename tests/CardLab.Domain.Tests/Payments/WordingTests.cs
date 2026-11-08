using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Payments;

public sealed class WordingTests
{
    private static readonly PaymentReference Ref = new("PAY-7K3M9Q2T");

    // T-U-3 · PR-01 · unit
    // Given wording v1, When NothingToPay, Then exactly "Nothing to pay. Your card balance is £0.00."
    // Catches: W-7 drift from the approved text (AC-5, NFR-7)
    [Fact]
    public void Nothing_to_pay_is_W7()
    {
        Assert.Equal("Nothing to pay. Your card balance is £0.00.", WordingV1.NothingToPay());
    }

    // T-U-5 · PR-01 · unit
    // Given £150.50, from "Brightline Supplies Ltd current account, ending 7731", to "Ops van card, ending 0001", When ReviewScreen, Then version "v1", heading "Pay your full balance", exactly the six W-1 lines in order and button "Confirm payment of £150.50"
    // Catches: missing fee/total/execution-time line or reordered lines on the one review screen (AC-6, R-4, OB-1, OB-7)
    [Fact]
    public void Review_screen_is_W1_exactly()
    {
        var screen = WordingV1.ReviewScreen(
            TestMoney.Of("150.50"),
            "Brightline Supplies Ltd current account, ending 7731",
            "Ops van card, ending 0001");

        Assert.Equal("v1", screen.Version);
        Assert.Equal("Pay your full balance", screen.Heading);
        Assert.Equal(
            [
                "Amount: £150.50",
                "Fee: £0.00 (no fee)",
                "Total to pay: £150.50",
                "When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.",
                "From: Brightline Supplies Ltd current account, ending 7731",
                "To: Ops van card, ending 0001",
            ],
            screen.Lines);
        Assert.Equal("Confirm payment of £150.50", screen.ButtonLabel);
    }

    // T-U-32 · PR-01 · unit
    // Given amount £150.50, balances £0.00 / £150.50 / £170.50 / £1,000,000.01 and reference PAY-7K3M9Q2T, When Completed, RefusedInsufficientFunds, RefusedAccountUnavailable, Pending, BalanceChanged, AboveLimit, PendingExists, StillPending, Then each equals the §17 text W-2, W-3, W-4, W-5, W-6, W-8, W-9, W-10 character for character
    // Catches: any wording drift from the approved v1 (AC-7, AC-8, AC-11, AC-12, AC-13, AC-16, AC-24, AC-29, AC-35, NFR-7)
    [Fact]
    public void Outcome_texts_are_wording_v1()
    {
        var amount = TestMoney.Of("150.50");

        Assert.Equal(
            "Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference PAY-7K3M9Q2T.",
            WordingV1.Completed(amount, TestMoney.Of("0.00"), Ref));
        Assert.Equal(
            "Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still £150.50. Add money to the account, then try again. Reference PAY-7K3M9Q2T.",
            WordingV1.RefusedInsufficientFunds(TestMoney.Of("150.50"), Ref));
        Assert.Equal(
            "Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference PAY-7K3M9Q2T.",
            WordingV1.RefusedAccountUnavailable(TestMoney.Of("150.50"), Ref));
        Assert.Equal(
            "Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference PAY-7K3M9Q2T.",
            WordingV1.Pending(TestMoney.Of("150.50"), Ref));
        Assert.Equal(
            "Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.",
            WordingV1.BalanceChanged(TestMoney.Of("170.50")));
        Assert.Equal(
            "This balance cannot be paid here. Payments above £1,000,000.00 are not available online. No money has moved. Your card balance is £1,000,000.01. Contact your account administrator.",
            WordingV1.AboveLimit(TestMoney.Of("1000000.01")));
        Assert.Equal(
            "A payment of £150.50 (reference PAY-7K3M9Q2T) is still pending. You cannot make another payment until we know its outcome. Choose Check status.",
            WordingV1.PendingExists(amount, Ref));
        Assert.Equal(
            "Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference PAY-7K3M9Q2T.",
            WordingV1.StillPending(Ref));
    }

    // T-U-36 · PR-01 · unit
    // Given each state/reason, When Receipt, Then Completed → W-2, Failed InsufficientFunds → W-3, Failed AccountUnavailable → W-4, Failed NotReceived → W-4, Pending → W-5, Failed with a null reason throws ArgumentException; and Label("Ops van card", "0001") is "Ops van card, ending 0001"
    // Catches: a not-received failure shown with the wrong text, or labels unlike W-1 (design §9, AC-6, AC-15)
    [Fact]
    public void Receipt_message_follows_state_and_reason()
    {
        var amount = TestMoney.Of("150.50");
        var balance = TestMoney.Of("150.50");
        var paid = TestMoney.Of("0.00");

        Assert.Equal(WordingV1.Completed(amount, paid, Ref), WordingV1.Receipt(PaymentState.Completed, null, amount, paid, Ref));
        Assert.Equal(WordingV1.RefusedInsufficientFunds(balance, Ref), WordingV1.Receipt(PaymentState.Failed, FailureReason.InsufficientFunds, amount, balance, Ref));
        Assert.Equal(WordingV1.RefusedAccountUnavailable(balance, Ref), WordingV1.Receipt(PaymentState.Failed, FailureReason.AccountUnavailable, amount, balance, Ref));
        Assert.Equal(WordingV1.RefusedAccountUnavailable(balance, Ref), WordingV1.Receipt(PaymentState.Failed, FailureReason.NotReceived, amount, balance, Ref));
        Assert.Equal(WordingV1.Pending(balance, Ref), WordingV1.Receipt(PaymentState.Pending, null, amount, balance, Ref));
        Assert.Throws<ArgumentException>(() => WordingV1.Receipt(PaymentState.Failed, null, amount, balance, Ref));
        Assert.Equal("Ops van card, ending 0001", WordingV1.Label("Ops van card", "0001"));
    }

    // Added: the four receipt branches must be distinguishable by their literal text, not only by delegation.
    [Fact]
    public void Receipt_texts_differ_by_branch()
    {
        var amount = TestMoney.Of("150.50");
        var texts = new[]
        {
            WordingV1.Receipt(PaymentState.Completed, null, amount, Money.Zero, Ref),
            WordingV1.Receipt(PaymentState.Failed, FailureReason.InsufficientFunds, amount, amount, Ref),
            WordingV1.Receipt(PaymentState.Failed, FailureReason.NotReceived, amount, amount, Ref),
            WordingV1.Receipt(PaymentState.Pending, null, amount, amount, Ref),
        };

        Assert.StartsWith("Payment complete.", texts[0]);
        Assert.StartsWith("Payment not made. Your bank refused it", texts[1]);
        Assert.StartsWith("Payment not made. Your bank could not process it", texts[2]);
        Assert.StartsWith("Payment pending.", texts[3]);
    }
}
