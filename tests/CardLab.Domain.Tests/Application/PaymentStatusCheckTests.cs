namespace CardLab.Domain.Tests.Application;

public sealed class PaymentStatusCheckTests
{
    // T-U-16 · PR-04 · unit (application)
    // Given a pending £150.50 payment after Hang(debitApplied: true) at T1, When CheckStatusAsync, Then Completed with EffectiveAt = T1, BalanceOwed £0.00, W-2, a StatusCheckCompleted event, the bank entry still single, and QuoteAsync is allowed again
    // Catches: a payment the bank applied staying unpaid, or the card staying blocked (AC-14, AC-34, R-9, R-11)
    [Fact(Skip = "todo")]
    public void Check_settles_completed_with_the_bank_debit_time()
    {
    }

    // T-U-17 · PR-04 · unit (application)
    // Given a pending payment after Hang(debitApplied: false), When CheckStatusAsync (bank answers NotReceived), Then Failed with Reason NotReceived and the W-4 text, balance £150.50, a StatusCheckNotReceived event, and the card may pay again
    // Catches: a never-received debit left pending forever or counted as paid (AC-15, R-11)
    [Fact(Skip = "todo")]
    public void Check_fails_a_debit_the_bank_never_received()
    {
    }

    // T-U-18 · PR-04 · unit (application)
    // Given a pending payment and bank.HangStatus(), When CheckStatusAsync, Then still Pending with W-10, a StatusCheckUnanswered event, a BankCallUnanswered log with call "status", and DebitRequests still 1
    // Catches: an unanswered check failing the payment or re-sending the debit (AC-16, R-11)
    [Fact(Skip = "todo")]
    public void Unanswered_check_stays_pending_without_a_new_debit()
    {
    }

    // T-U-21 · PR-04 · unit (application)
    // Given a pending £150.50 payment on Ops van and then an approved £20.00 purchase, When the bank reports completed and CheckStatusAsync runs, Then BalanceOwed £20.00 and the settlement has amount £150.50, BalanceBefore £170.50 and BalanceAfter £20.00
    // Catches: before/after balances computed from stale sums (AC-36, R-14)
    [Fact(Skip = "todo")]
    public void Settlement_records_balances_around_the_settled_amount()
    {
    }

    // T-U-22 · PR-04 · unit (application)
    // Given a pending payment and NextStatus(Completed(null)), When CheckStatusAsync at clock T2, Then Completed with EffectiveAt = SettledAt = T2 and no BankDebitedAt
    // Catches: a missing bank debit time leaving the effective time empty (AC-38, R-11)
    [Fact(Skip = "todo")]
    public void Effective_time_falls_back_to_the_settle_time()
    {
    }

    // T-U-39 · PR-04 · unit (application)
    // Given a completed payment, When CheckStatusAsync, Then the stored receipt, no bank call and no new event; an unknown reference → ReferenceNotFound
    // Catches: status checks re-settling a terminal payment (R-6)
    [Fact(Skip = "todo")]
    public void Check_on_a_settled_payment_is_a_no_op()
    {
    }
}
