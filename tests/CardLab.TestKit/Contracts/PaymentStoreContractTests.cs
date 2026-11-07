using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using Xunit;

namespace CardLab.TestKit.Contracts;

/// <summary>
/// 002: the payment behaviour every <see cref="ICardStore"/> must have (design §15.2), run against the in-memory store
/// (T-U-23, <c>tests/CardLab.Domain.Tests/Fakes/InMemoryPaymentStoreContractTests.cs</c>) and SQL Server (T-I-16,
/// <c>tests/CardLab.Integration.Tests/Store/SqlPaymentStoreContractTests.cs</c>). Sibling of <see cref="CardStoreContractTests"/>.
/// Bodies written in PR-02 (against the in-memory store); PR-05 makes them pass against SQL without editing this file.
/// Tests arrange through the store's own API (WithCardLockAsync + Payments, AppendTransaction) and assert only on their own cards.
/// </summary>
public abstract class PaymentStoreContractTests
{
    /// <summary>Creates a store containing (at least) the given cards with no transactions or payments, and the given linked account.</summary>
    /// <param name="cards">Cards to seed (use <c>CardBuilder</c>).</param>
    /// <param name="account">Linked account (use <c>LinkedAccountBuilder</c>).</param>
    /// <returns>The store under test.</returns>
    protected abstract Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards, LinkedAccount account);

    /// <summary>Opens another handle on the same data (in-memory: the same instance; SQL: a new store on a new DbContext).</summary>
    /// <param name="seeded">A store returned by <see cref="CreateStoreAsync"/>.</param>
    /// <returns>A store over the same data.</returns>
    protected abstract Task<ICardStore> OpenStoreAsync(ICardStore seeded);

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a store created with the seeded linked account, When GetLinkedAccountAsync, Then the same id, display name, last four 7731, bank ref and authority basis
    // Catches: the adapter losing the account or its authority text (R-2)
    /// <summary>Contract P-1.</summary>
    [Fact(Skip = "todo")]
    public Task Reads_the_linked_account() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given approved purchases £120.00 and £30.50, a declined £500.00, and payments of £100.00 completed, £10.00 failed and £5.00 pending, When GetBalanceAsync and (inside the lock) SumApprovedPurchasesAsync / SumCompletedPaymentsAsync, Then purchases £150.50, completed £100.00, owed £50.50; an unknown card gives null
    // Catches: declined purchases or non-completed payments counted (AC-1, AC-3, R-1)
    /// <summary>Contract P-2.</summary>
    [Fact(Skip = "todo")]
    public Task Balance_sums_approved_purchases_and_completed_payments() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a quote appended in one unit of work, When GetQuoteAsync (same card) and GetPaymentRecordAsync, Then every field round-trips exactly (amounts, labels, actor, authority, CreatedAtUtc, Shown version/heading/lines in order/button, origin) with status Open and no payment; GetQuoteAsync under another card's lock gives null
    // Catches: shown-and-accepted fields altered on store, or another card's reference usable (R-7, R-14, AC-20)
    /// <summary>Contract P-3.</summary>
    [Fact(Skip = "todo")]
    public Task Quote_round_trips_and_is_scoped_to_its_card() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a stored quote, When SetQuoteStatus(Refused, "balance_changed", £170.50, t), Then the record shows status Refused, RefusalReason, BalanceAtRefusal and every shown field unchanged
    // Catches: refusal overwriting what was shown (R-5, R-14)
    /// <summary>Contract P-4.</summary>
    [Fact(Skip = "todo")]
    public Task Refusal_changes_only_status_columns() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a quote, When CreatePayment(Pending) and commit, Then GetPendingPaymentAsync returns it (id, reference, amount, created time, no settlement) and GetQuoteAsync's Payment is the same snapshot
    // Catches: pending payment not visible to the one-in-flight check (R-8)
    /// <summary>Contract P-5.</summary>
    [Fact(Skip = "todo")]
    public Task Created_payment_is_pending_and_linked_to_its_quote() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a pending payment on a card, When another unit of work creates a second pending payment (other reference) on the same card, Then the unit of work throws and nothing from it is stored
    // Catches: the store accepting two pending payments per card if the rules are bypassed (R-8, UX_Payment_OnePending)
    /// <summary>Contract P-6.</summary>
    [Fact(Skip = "todo")]
    public Task Rejects_a_second_pending_payment_on_a_card() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a payment for PAY-00000001, When another unit of work creates a payment for PAY-00000001, Then it throws and only one payment exists
    // Catches: two payments for one reference (R-7)
    /// <summary>Contract P-7.</summary>
    [Fact(Skip = "todo")]
    public Task Rejects_a_second_payment_for_one_reference() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a pending payment, When SettlePayment(Completed, settled/effective/debited times, before £150.50, after £0.00, simulated true), Then the snapshot carries the settlement, it is no longer pending and SumCompletedPaymentsAsync includes it; a Failed settlement is excluded from the sum
    // Catches: settlement fields lost or failed payments counted as paid (R-6, R-14, AC-36)
    /// <summary>Contract P-8.</summary>
    [Fact(Skip = "todo")]
    public Task Settlement_is_stored_and_counted_only_when_completed() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given three references on a card (open, refused, completed with events appended in order), When ListPaymentRecordsAsync and GetPaymentRecordAsync, Then records newest first by quote time, events oldest first with kind, from/to, detail and simulated flag
    // Catches: refused or open references missing from the record list, or events reordered (R-14, AC-20)
    /// <summary>Contract P-9.</summary>
    [Fact(Skip = "todo")]
    public Task Records_list_every_reference_with_events_in_order() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given purchases and payments at interleaved times plus an open quote without a payment, When ListActivityAsync(card, take), Then purchases and payments merged newest first (purchase fields or payment state/reference/origin set), the quote absent, and at most take rows
    // Catches: payments missing from history or purchases shown as payments (R-13, AC-2)
    /// <summary>Contract P-10.</summary>
    [Fact(Skip = "todo")]
    public Task Activity_merges_purchases_and_payments_newest_first() => Task.CompletedTask;

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a unit of work that appends a quote, creates a payment and an event and then throws, When it ends, Then none of them are stored
    // Catches: partial payment writes surviving a failed unit of work (R-9)
    /// <summary>Contract P-11.</summary>
    [Fact(Skip = "todo")]
    public Task Payment_writes_are_discarded_when_the_work_throws() => Task.CompletedTask;
}
