namespace CardLab.Domain.Tests.Application;

public sealed class PaymentServiceTests
{
    // T-U-25 · PR-04 · unit (application)
    // Given Ops van with approved purchases £150.50 and the seeded linked account, When QuoteAsync(card, DeploySmoke), Then Ok with reference PAY-00000001, W-1 wording for £150.50, actor Cardholder "Priya Nair" with Id = card id, origin DeploySmoke, and the store holds one Open quote; at £0.00 → NothingToPay(W-7), at £1,000,000.01 → AboveLimit(W-8), with a pending payment → PaymentPending(W-9), unknown card → CardNotFound, each storing nothing
    // Catches: quotes stored when payment is unavailable, or the admin recorded as actor (AC-4, AC-5, AC-6, AC-11, AC-24, R-16)
    [Fact(Skip = "todo")]
    public void Quote_stores_the_review_or_refuses_with_the_right_message()
    {
    }

    // T-U-10 · PR-04 · unit (application)
    // Given a £150.50 quote and the bank completing naturally, When ConfirmAsync, Then Ok receipt Completed, fee £0.00, Time = effective time, W-2 with £150.50 and £0.00, BalanceOwed £0.00, Repeated false; store balance £0.00; quote Confirmed; events ConfirmCreated, DebitRequested, BankCompleted in order; bank available £24,849.50 with one entry for the reference
    // Catches: a confirm that moves money on one side only, or a receipt that misstates the outcome (AC-7, R-9, R-12)
    [Fact(Skip = "todo")]
    public void Confirm_completes_and_updates_both_sides()
    {
    }

    // T-U-11 · PR-04 · unit (application)
    // Given a £150.50 quote and then a £20.00 approved purchase on the card, When ConfirmAsync, Then BalanceChanged with W-6 for £170.50 and BalanceOwed £170.50; quote Refused with reason balance_changed and BalanceAtRefusal £170.50; one ConfirmRefused event; no payment; the bank received no request
    // Catches: paying a different amount from the one reviewed (AC-8, R-5, OB-7)
    [Fact(Skip = "todo")]
    public void Balance_change_refuses_and_records_the_attempt()
    {
    }

    // T-U-19 · PR-04 · unit (application)
    // Given a reference refused for balance_changed, When ConfirmAsync again, Then QuoteRefused with the same W-6 text for the current balance, a second ConfirmRefused event, still no payment; and a new QuoteAsync returns a different reference
    // Catches: a refused reference later starting a payment (AC-30, R-7)
    [Fact(Skip = "todo")]
    public void Refused_reference_stays_refused()
    {
    }

    // T-U-12 · PR-04 · unit (application)
    // Given a completed confirm, When ConfirmAsync twice more for the same reference, Then each returns the original receipt with Repeated true; one payment; one DebitRequest at the bank; two ConfirmRepeated events with their times
    // Catches: double click or retry creating a second payment or debit (AC-9, R-7)
    [Fact(Skip = "todo")]
    public void Repeated_confirm_returns_the_original_receipt()
    {
    }

    // T-U-13 · PR-04 · unit (application)
    // Given the bank holding debits until a gate, When two ConfirmAsync for one reference run concurrently and the gate is then released, Then both receipts are Completed with the same reference, amount and time (one Repeated), one payment and one DebitRequest
    // Catches: concurrent confirms racing into two payments or two different answers (AC-10, DEC-5)
    [Fact(Skip = "todo")]
    public void Concurrent_confirms_share_one_payment()
    {
    }

    // T-U-14 · PR-04 · unit (application)
    // Given a pending payment PAY-00000001 of £150.50 (bank Hang) and an open quote PAY-00000002 minted before it, When QuoteAsync and ConfirmAsync(PAY-00000002), Then both fail PaymentPending with W-9 naming £150.50 and PAY-00000001; nothing new is stored
    // Catches: a second payment in flight on one card (AC-11, R-8)
    [Fact(Skip = "todo")]
    public void Pending_payment_blocks_new_quotes_and_confirms()
    {
    }

    // T-U-15 · PR-04 · unit (application)
    // Given a £150.50 quote and NextDebit(InsufficientFunds), When ConfirmAsync, Then receipt Failed, Reason InsufficientFunds, fee £0.00, W-3 with £150.50; balance £150.50; bank balance and entries unchanged; events ConfirmCreated, DebitRequested, BankRefused; a new quote is allowed
    // Catches: a refused debit reducing the balance or blocking later payments (AC-12, R-10)
    [Fact(Skip = "todo")]
    public void Insufficient_funds_fails_and_nothing_moves()
    {
    }

    // T-U-20 · PR-04 · unit (application)
    // Given a £150.50 quote and NextDebit(AccountUnavailable), When ConfirmAsync, Then Failed with AccountUnavailable, fee £0.00 and W-4 with £150.50; nothing moved
    // Catches: the wrong refusal text (AC-35, R-10)
    [Fact(Skip = "todo")]
    public void Account_unavailable_fails_with_W4()
    {
    }

    // T-U-26 · PR-04 · unit (application)
    // Given a £150.50 quote and bank.Hang(), When ConfirmAsync, Then receipt Pending with W-5 (balance £150.50) and Time = acceptance time; events ConfirmCreated, DebitRequested, BankUnanswered; one BankCallUnanswered log with call "debit"; PaymentStateChanged only for none → pending
    // Catches: an unanswered debit treated as failed (money may have moved) or retried (AC-13, R-11)
    [Fact(Skip = "todo")]
    public void Unanswered_debit_leaves_the_payment_pending()
    {
    }

    // T-U-27 · PR-04 · unit (application)
    // Given bank.FailNext(new InvalidOperationException()), When ConfirmAsync, Then the payment stays Pending with W-5 and a BankUnanswered event whose Detail is "InvalidOperationException"; no exception escapes
    // Catches: an adapter error failing a payment the bank may have applied (design §9)
    [Fact(Skip = "todo")]
    public void Adapter_error_is_treated_as_unknown()
    {
    }

    // T-U-28 · PR-04 · unit (application)
    // Given a completed confirm, a repeat and a balance-changed refusal, When the logger is read, Then exactly two PaymentStateChanged (none→pending, pending→completed) with PaymentId, CardId, LastFour 0001, Kind full, Amount 150.50, FromState, ToState, Reason "-" and DurationMs; PaymentConfirmAttempt results created, repeated, refused_balance_changed; no field contains a 16-digit run
    // Catches: missing, duplicated or number-leaking log events (AC-27, NFR-4, NFR-3)
    [Fact(Skip = "todo")]
    public void Logs_one_state_change_event_per_change()
    {
    }

    // T-U-31 · PR-04 · unit (application)
    // Given a store decorator whose settle lock (second and third WithCardLockAsync of a confirm) throws CardLockTimeoutException, options SettleLockRetryMs 0, When ConfirmAsync with a completing bank, Then the receipt is Pending (W-5) and the best-effort fourth lock appended a BankUnanswered event with Detail "settle_lock_timeout"; when it throws only once, the retry settles Completed
    // Catches: a lock timeout after the bank answered losing the outcome or failing the payment (design §9)
    [Fact(Skip = "todo")]
    public void Settle_lock_timeout_retries_once_then_stays_pending()
    {
    }

    // T-U-38 · PR-04 · unit (application)
    // Given a completed payment, When GetReceiptAsync, Then the stored receipt (Repeated false) and no bank call; another card's reference or an open quote without a payment → ReferenceNotFound; unknown card → CardNotFound
    // Catches: receipt reload calling the bank or exposing another card's payment (design §7)
    [Fact(Skip = "todo")]
    public void Receipt_reload_reads_without_calling_the_bank()
    {
    }
}
