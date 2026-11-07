namespace CardLab.Domain.Tests.Payments;

public sealed class WordingTests
{
    // T-U-3 · PR-01 · unit
    // Given wording v1, When NothingToPay, Then exactly "Nothing to pay. Your card balance is £0.00."
    // Catches: W-7 drift from the approved text (AC-5, NFR-7)
    [Fact(Skip = "todo")]
    public void Nothing_to_pay_is_W7()
    {
    }

    // T-U-5 · PR-01 · unit
    // Given £150.50, from "Brightline Supplies Ltd current account, ending 7731", to "Ops van card, ending 0001", When ReviewScreen, Then version "v1", heading "Pay your full balance", exactly the six W-1 lines in order and button "Confirm payment of £150.50"
    // Catches: missing fee/total/execution-time line or reordered lines on the one review screen (AC-6, R-4, OB-1, OB-7)
    [Fact(Skip = "todo")]
    public void Review_screen_is_W1_exactly()
    {
    }

    // T-U-32 · PR-01 · unit
    // Given amount £150.50, balances £0.00 / £150.50 / £170.50 / £1,000,000.01 and reference PAY-7K3M9Q2T, When Completed, RefusedInsufficientFunds, RefusedAccountUnavailable, Pending, BalanceChanged, AboveLimit, PendingExists, StillPending, Then each equals the §17 text W-2, W-3, W-4, W-5, W-6, W-8, W-9, W-10 character for character
    // Catches: any wording drift from the approved v1 (AC-7, AC-8, AC-11, AC-12, AC-13, AC-16, AC-24, AC-29, AC-35, NFR-7)
    [Fact(Skip = "todo")]
    public void Outcome_texts_are_wording_v1()
    {
    }

    // T-U-36 · PR-01 · unit
    // Given each state/reason, When Receipt, Then Completed → W-2, Failed InsufficientFunds → W-3, Failed AccountUnavailable → W-4, Failed NotReceived → W-4, Pending → W-5, Failed with a null reason throws ArgumentException; and Label("Ops van card", "0001") is "Ops van card, ending 0001"
    // Catches: a not-received failure shown with the wrong text, or labels unlike W-1 (design §9, AC-6, AC-15)
    [Fact(Skip = "todo")]
    public void Receipt_message_follows_state_and_reason()
    {
    }
}
