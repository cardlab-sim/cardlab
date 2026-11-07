using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Limits;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Builders;
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
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
    private static int nextBlock;

    // xUnit creates one instance per test: each test gets its own block of references and its own account id, so
    // cases never collide on keys in a database shared by the whole class (counters, never random).
    private readonly int block = Interlocked.Increment(ref nextBlock);

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
    // Given a store created with the seeded linked account, When GetLinkedAccountAsync, Then a non-empty id, display name, last four 7731, bank ref and authority basis
    // Catches: the adapter losing the account or its authority text (R-2)
    /// <summary>Contract P-1.</summary>
    [Fact]
    public async Task Reads_the_linked_account()
    {
        var store = await CreateStoreAsync([new CardBuilder().Build()], NewAccount());

        var read = await store.GetLinkedAccountAsync();

        // Only fields every LinkedAccount row in a shared database has (the builder defaults equal the seed).
        Assert.NotEqual(Guid.Empty, read!.Id);
        Assert.Equal(Seed.LinkedAccountName, read.DisplayName);
        Assert.Equal("7731", read.LastFour);
        Assert.Equal(new BankAccountRef(Seed.BankAccountRef), read.BankRef);
        Assert.Equal(Seed.AuthorityBasis, read.AuthorityBasis);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given approved purchases £120.00 and £30.50, a declined £500.00, and payments of £100.00 completed, £10.00 failed and £5.00 pending, When GetBalanceAsync and (inside the lock) SumApprovedPurchasesAsync / SumCompletedPaymentsAsync, Then purchases £150.50, completed £100.00, owed £50.50; an unknown card gives null
    // Catches: declined purchases or non-completed payments counted (AC-1, AC-3, R-1)
    /// <summary>Contract P-2.</summary>
    [Fact]
    public async Task Balance_sums_approved_purchases_and_completed_payments()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.AppendTransaction(new TransactionBuilder().Amount("120.00").At(T0).Build());
            w.AppendTransaction(new TransactionBuilder().Amount("30.50").At(T0.AddMinutes(1)).Build());
            w.AppendTransaction(new TransactionBuilder().Amount("500.00").At(T0.AddMinutes(2)).Declined(Reason.LimitExceeded).Build());
            return Task.FromResult(0);
        });
        await PayAsync(store, card, Ref(1).Value, "100.00", T0, p => p.Completed("150.50", "50.50"));
        await PayAsync(store, card, Ref(2).Value, "10.00", T0.AddMinutes(10), p => p.Failed(FailureReason.InsufficientFunds, "50.50"));
        await PayAsync(store, card, Ref(3).Value, "5.00", T0.AddMinutes(20), null);

        var balance = await store.GetBalanceAsync(card.Id);
        var (purchases, completed) = await store.WithCardLockAsync(card.Id, async w =>
            (await w.Payments.SumApprovedPurchasesAsync(), await w.Payments.SumCompletedPaymentsAsync()));
        var unknown = await store.GetBalanceAsync(new CardId(new Guid("0badc0de-0000-0000-0000-000000000002")));

        Assert.Equal(("150.50", "100.00", "50.50"), (balance!.ApprovedPurchases.ToString(), balance.CompletedPayments.ToString(), balance.Owed.ToString()));
        Assert.Equal(("150.50", "100.00"), (purchases.ToString(), completed.ToString()));
        Assert.Null(unknown);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a quote appended in one unit of work, When GetQuoteAsync (same card) and GetPaymentRecordAsync, Then every field round-trips exactly (amounts, labels, actor, authority, CreatedAtUtc, Shown version/heading/lines in order/button, origin) with status Open and no payment; GetQuoteAsync under another card's lock gives null
    // Catches: shown-and-accepted fields altered on store, or another card's reference usable (R-7, R-14, AC-20)
    /// <summary>Contract P-3.</summary>
    [Fact]
    public async Task Quote_round_trips_and_is_scoped_to_its_card()
    {
        var card = new CardBuilder().Build();
        var other = new CardBuilder().Build();
        var store = await CreateStoreAsync([card, other], NewAccount());
        var quote = new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).WithAmount("170.50").WithOrigin(PaymentOrigin.DeploySmoke).At(T0).Build();
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendQuote(quote);
            return Task.FromResult(0);
        });

        var own = await store.WithCardLockAsync(card.Id, w => w.Payments.GetQuoteAsync(quote.Reference));
        var foreign = await store.WithCardLockAsync(other.Id, w => w.Payments.GetQuoteAsync(quote.Reference));
        var record = await store.GetPaymentRecordAsync(quote.Reference);

        AssertSameQuote(quote, own!.Quote);
        Assert.Equal(QuoteStatus.Open, own.Status);
        Assert.Null(own.Payment);
        Assert.Null(foreign);
        AssertSameQuote(quote, record!.Quote);
        Assert.Equal(QuoteStatus.Open, record.Status);
        Assert.Null(record.Payment);
        Assert.Empty(record.Events);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a stored quote, When SetQuoteStatus(Refused, "balance_changed", £170.50, t), Then the record shows status Refused, RefusalReason, BalanceAtRefusal and every shown field unchanged
    // Catches: refusal overwriting what was shown (R-5, R-14)
    /// <summary>Contract P-4.</summary>
    [Fact]
    public async Task Refusal_changes_only_status_columns()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        var quote = new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).At(T0).Build();
        await AppendQuoteAsync(store, card, quote);

        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.SetQuoteStatus(quote.Reference, QuoteStatus.Refused, "balance_changed", TestMoney.Of("170.50"), T0.AddMinutes(1));
            return Task.FromResult(0);
        });
        var record = await store.GetPaymentRecordAsync(quote.Reference);

        Assert.Equal(QuoteStatus.Refused, record!.Status);
        Assert.Equal("balance_changed", record.RefusalReason);
        Assert.Equal("170.50", record.BalanceAtRefusal!.Value.ToString());
        AssertSameQuote(quote, record.Quote);
        Assert.Null(record.Payment);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a quote, When CreatePayment(Pending) and commit, Then GetPendingPaymentAsync returns it (id, reference, amount, created time, no settlement) and GetQuoteAsync's Payment is the same snapshot
    // Catches: pending payment not visible to the one-in-flight check (R-8)
    /// <summary>Contract P-5.</summary>
    [Fact]
    public async Task Created_payment_is_pending_and_linked_to_its_quote()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        var quote = new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).At(T0).Build();
        var created = new PaymentBuilder().ForQuote(quote).CreatedAt(T0.AddMinutes(1));
        await AppendQuoteAsync(store, card, quote);

        var id = await store.WithCardLockAsync(card.Id, w => Task.FromResult(w.Payments.CreatePayment(created.BuildNew())));
        var (pending, linked) = await store.WithCardLockAsync(card.Id, async w =>
            (await w.Payments.GetPendingPaymentAsync(), (await w.Payments.GetQuoteAsync(quote.Reference))!.Payment));

        Assert.Equal(id, pending!.Id);
        Assert.Equal((Ref(1).Value, PaymentState.Pending, "150.50", T0.AddMinutes(1)), (pending.Reference.Value, pending.State, pending.Amount.ToString(), pending.CreatedAtUtc));
        Assert.Null(pending.Settlement);
        Assert.Equal(pending, linked);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a pending payment on a card, When another unit of work creates a second pending payment (other reference) on the same card, Then the unit of work throws and nothing from it is stored
    // Catches: the store accepting two pending payments per card if the rules are bypassed (R-8, UX_Payment_OnePending)
    /// <summary>Contract P-6.</summary>
    [Fact]
    public async Task Rejects_a_second_pending_payment_on_a_card()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await PayAsync(store, card, Ref(1).Value, "150.50", T0, null);
        var second = new QuoteBuilder().ForCard(card).WithReference(Ref(2).Value).At(T0.AddMinutes(5)).Build();

        await Assert.ThrowsAnyAsync<Exception>(() => store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendQuote(second);
            w.Payments.CreatePayment(new PaymentBuilder().ForQuote(second).CreatedAt(T0.AddMinutes(6)).BuildNew());
            return Task.FromResult(0);
        }));

        Assert.Null(await store.GetPaymentRecordAsync(second.Reference));
        Assert.Equal([Ref(1).Value], (await store.ListPaymentRecordsAsync(card.Id)).Select(r => r.Quote.Reference.Value));
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a payment for PAY-00000001, When another unit of work creates a payment for PAY-00000001, Then it throws and only one payment exists
    // Catches: two payments for one reference (R-7)
    /// <summary>Contract P-7.</summary>
    [Fact]
    public async Task Rejects_a_second_payment_for_one_reference()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await PayAsync(store, card, Ref(1).Value, "150.50", T0, p => p.Completed());
        var again = new PaymentBuilder().ForQuote(new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).Build()).CreatedAt(T0.AddMinutes(5));

        await Assert.ThrowsAnyAsync<Exception>(() => store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.CreatePayment(again.BuildNew());
            return Task.FromResult(0);
        }));

        var activity = await store.ListActivityAsync(card.Id, 10);
        Assert.Single(activity, a => a.Kind == ActivityKind.Payment);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a pending payment, When SettlePayment(Completed, settled/effective/debited times, before £150.50, after £0.00, simulated true), Then the snapshot carries the settlement, it is no longer pending and SumCompletedPaymentsAsync includes it; a Failed settlement is excluded from the sum
    // Catches: settlement fields lost or failed payments counted as paid (R-6, R-14, AC-36)
    /// <summary>Contract P-8.</summary>
    [Fact]
    public async Task Settlement_is_stored_and_counted_only_when_completed()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        var settlement = new Settlement(
            PaymentState.Completed,
            null,
            T0.AddMinutes(2),
            T0.AddMinutes(1).AddSeconds(30),
            T0.AddMinutes(1).AddSeconds(30),
            TestMoney.Of("150.50"),
            TestMoney.Of("0.00"),
            true);
        var id = await PayAsync(store, card, Ref(1).Value, "150.50", T0, null);

        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.SettlePayment(id, settlement);
            return Task.FromResult(0);
        });
        var (snapshot, pending, completed) = await store.WithCardLockAsync(card.Id, async w =>
            ((await w.Payments.GetQuoteAsync(Ref(1)))!.Payment, await w.Payments.GetPendingPaymentAsync(), await w.Payments.SumCompletedPaymentsAsync()));

        Assert.Equal(PaymentState.Completed, snapshot!.State);
        Assert.Equal(settlement, snapshot.Settlement);
        Assert.Null(pending);
        Assert.Equal("150.50", completed.ToString());

        await PayAsync(store, card, Ref(2).Value, "20.00", T0.AddMinutes(10), p => p.Failed(FailureReason.AccountUnavailable, "20.00"));
        var afterFailure = await store.WithCardLockAsync(card.Id, w => w.Payments.SumCompletedPaymentsAsync());
        var failed = (await store.GetPaymentRecordAsync(Ref(2)))!.Payment;
        Assert.Equal("150.50", afterFailure.ToString());
        Assert.Equal((PaymentState.Failed, FailureReason.AccountUnavailable), (failed!.State, failed.Settlement!.Reason));
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given three references on a card (open, refused, completed with events appended in order), When ListPaymentRecordsAsync and GetPaymentRecordAsync, Then records newest first by quote time, events oldest first with kind, from/to, detail and simulated flag
    // Catches: refused or open references missing from the record list, or events reordered (R-14, AC-20)
    /// <summary>Contract P-9.</summary>
    [Fact]
    public async Task Records_list_every_reference_with_events_in_order()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await AppendQuoteAsync(store, card, new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).At(T0).Build());
        var refused = new QuoteBuilder().ForCard(card).WithReference(Ref(2).Value).At(T0.AddMinutes(1)).Build();
        await AppendQuoteAsync(store, card, refused);
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.SetQuoteStatus(refused.Reference, QuoteStatus.Refused, "balance_changed", TestMoney.Of("170.50"), T0.AddMinutes(2));
            return Task.FromResult(0);
        });
        var id = await PayAsync(store, card, Ref(3).Value, "150.50", T0.AddMinutes(3), null);
        var third = Ref(3);
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendEvent(new PaymentEvent(third, id, T0.AddMinutes(4), PaymentEventKind.ConfirmCreated, null, PaymentState.Pending, null, false));
            w.Payments.AppendEvent(new PaymentEvent(third, id, T0.AddMinutes(5), PaymentEventKind.DebitRequested, null, null, null, false));
            w.Payments.AppendEvent(new PaymentEvent(third, id, T0.AddMinutes(6), PaymentEventKind.BankCompleted, PaymentState.Pending, PaymentState.Completed, "steered", true));
            return Task.FromResult(0);
        });

        var records = await store.ListPaymentRecordsAsync(card.Id);
        var record = await store.GetPaymentRecordAsync(third);

        Assert.Equal([Ref(3).Value, Ref(2).Value, Ref(1).Value], records.Select(r => r.Quote.Reference.Value));
        Assert.Equal([QuoteStatus.Open, QuoteStatus.Refused, QuoteStatus.Open], records.Select(r => r.Status));
        Assert.Equal(
            [
                (PaymentEventKind.ConfirmCreated, (PaymentState?)null, (PaymentState?)PaymentState.Pending, (string?)null, false, T0.AddMinutes(4)),
                (PaymentEventKind.DebitRequested, null, null, null, false, T0.AddMinutes(5)),
                (PaymentEventKind.BankCompleted, PaymentState.Pending, PaymentState.Completed, "steered", true, T0.AddMinutes(6)),
            ],
            record!.Events.Select(e => (e.Kind, e.From, e.To, e.Detail, e.Simulated, e.AtUtc)));
        Assert.All(record.Events, e => Assert.Equal(id, e.Payment));
        Assert.Equal(records[0].Events, record.Events);
        Assert.Empty(records[1].Events);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given purchases and payments at interleaved times plus an open quote without a payment, When ListActivityAsync(card, take), Then purchases and payments merged newest first (purchase fields or payment state/reference/origin set), the quote absent, and at most take rows
    // Catches: payments missing from history or purchases shown as payments (R-13, AC-2)
    /// <summary>Contract P-10.</summary>
    [Fact]
    public async Task Activity_merges_purchases_and_payments_newest_first()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.AppendTransaction(new TransactionBuilder().Amount("10.00").At(T0.AddMinutes(1)).Build());
            w.AppendTransaction(new TransactionBuilder().Amount("20.00").At(T0.AddMinutes(5)).Declined(Reason.LimitExceeded).Build());
            return Task.FromResult(0);
        });
        await PayAsync(store, card, Ref(1).Value, "150.50", T0.AddMinutes(3), p => p.Completed());
        await PayAsync(store, card, Ref(2).Value, "150.50", T0.AddMinutes(7), null);
        await AppendQuoteAsync(store, card, new QuoteBuilder().ForCard(card).WithReference(Ref(3).Value).At(T0.AddMinutes(8)).Build());

        var all = await store.ListActivityAsync(card.Id, 10);
        var newestTwo = await store.ListActivityAsync(card.Id, 2);

        var payment2 = new ActivityEntry(T0.AddMinutes(7).AddSeconds(30), ActivityKind.Payment, TestMoney.Of("150.50"), $"Payment {Ref(2)}", null, null, null, PaymentState.Pending, Ref(2), PaymentOrigin.CardholderView);
        var declined = new ActivityEntry(T0.AddMinutes(5), ActivityKind.Purchase, TestMoney.Of("20.00"), "Test Fuel Ltd", Outcome.Declined, Reason.LimitExceeded, Category.Fuel, null, null, null);
        var payment1 = new ActivityEntry(T0.AddMinutes(3).AddSeconds(30), ActivityKind.Payment, TestMoney.Of("150.50"), $"Payment {Ref(1)}", null, null, null, PaymentState.Completed, Ref(1), PaymentOrigin.CardholderView);
        var approved = new ActivityEntry(T0.AddMinutes(1), ActivityKind.Purchase, TestMoney.Of("10.00"), "Test Fuel Ltd", Outcome.Approved, Reason.Approved, Category.Fuel, null, null, null);
        Assert.Equal([payment2, declined, payment1, approved], all);
        Assert.Equal([payment2, declined], newestTwo);
    }

    // BUG-009 · PR-07f · contract
    // Given a purchase stamped by the processor before a payment but decided after it, When ListActivityAsync, Then it is listed above the payment (placed by decision time) with At = its processor stamp
    // Catches: showing the decision time (BUG-009); ordering purchases by the processor stamp
    /// <summary>Contract P-10b.</summary>
    [Fact]
    public async Task Late_purchase_shows_its_processor_time_ordered_by_decision()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        await PayAsync(store, card, Ref(1).Value, "150.50", T0.AddMinutes(3), p => p.Completed());
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.AppendTransaction(new TransactionBuilder().Amount("10.00").At(T0.AddMinutes(2)).DecidedAt(T0.AddMinutes(5)).Build());
            return Task.FromResult(0);
        });

        var activity = await store.ListActivityAsync(card.Id, 10);

        Assert.Equal([ActivityKind.Purchase, ActivityKind.Payment], activity.Select(a => a.Kind).ToArray());
        Assert.Equal(T0.AddMinutes(2), activity[0].At);
        Assert.Equal(T0.AddMinutes(3).AddSeconds(30), activity[1].At);
    }

    // T-U-23 / T-I-16 · PR-02 · contract
    // Given a unit of work that appends a quote, creates a payment and an event and then throws, When it ends, Then none of them are stored
    // Catches: partial payment writes surviving a failed unit of work (R-9)
    /// <summary>Contract P-11.</summary>
    [Fact]
    public async Task Payment_writes_are_discarded_when_the_work_throws()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], NewAccount());
        var quote = new QuoteBuilder().ForCard(card).WithReference(Ref(1).Value).At(T0).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WithCardLockAsync<int>(card.Id, w =>
        {
            w.Payments.AppendQuote(quote);
            var id = w.Payments.CreatePayment(new PaymentBuilder().ForQuote(quote).BuildNew());
            w.Payments.AppendEvent(new PaymentEvent(quote.Reference, id, T0, PaymentEventKind.ConfirmCreated, null, PaymentState.Pending, null, false));
            throw new InvalidOperationException("work failed");
        }));

        Assert.Null(await store.GetPaymentRecordAsync(quote.Reference));
        Assert.Empty(await store.ListPaymentRecordsAsync(card.Id));
        Assert.Empty(await store.ListActivityAsync(card.Id, 10));
        Assert.Null(await store.WithCardLockAsync(card.Id, w => w.Payments.GetPendingPaymentAsync()));
    }

    private PaymentReference Ref(int n) => new($"PAY-{block:D5}{n:D3}");

    private LinkedAccount NewAccount() => new LinkedAccountBuilder().WithId(new Guid(block, 0x1A, 0x1A, 0, 0, 0, 0, 0, 0, 0, 0)).Build();

    private static Task AppendQuoteAsync(ICardStore store, StoreSeedCard card, Quote quote) =>
        store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendQuote(quote);
            return Task.FromResult(0);
        });

    // Quote and pending payment in one unit of work, then (optionally) the settlement in a second one: SQL checks the
    // one-pending-per-card index at commit, so tests never rely on create-then-settle inside one unit.
    private static async Task<PaymentId> PayAsync(ICardStore store, StoreSeedCard card, string reference, string amount, DateTimeOffset at, Action<PaymentBuilder>? settle)
    {
        var quote = new QuoteBuilder().ForCard(card).WithReference(reference).WithAmount(amount).At(at).Build();
        var payment = new PaymentBuilder().ForQuote(quote).CreatedAt(at.AddSeconds(30));
        var id = await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendQuote(quote);
            return Task.FromResult(w.Payments.CreatePayment(payment.BuildNew()));
        });
        if (settle is not null)
        {
            settle(payment);
            await store.WithCardLockAsync(card.Id, w =>
            {
                w.Payments.SettlePayment(id, payment.BuildSettlement());
                return Task.FromResult(0);
            });
        }

        return id;
    }

    // Field by field: RenderedWording.Lines is a list, so record equality on the quote would compare references.
    private static void AssertSameQuote(Quote expected, Quote actual)
    {
        Assert.Equal(expected.Reference, actual.Reference);
        Assert.Equal(expected.Card, actual.Card);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal((expected.Amount, expected.Fee, expected.Total), (actual.Amount, actual.Fee, actual.Total));
        Assert.Equal((expected.FromLabel, expected.ToLabel), (actual.FromLabel, actual.ToLabel));
        Assert.Equal(expected.Actor, actual.Actor);
        Assert.Equal(expected.AuthorityBasis, actual.AuthorityBasis);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal((expected.Shown.Version, expected.Shown.Heading, expected.Shown.ButtonLabel), (actual.Shown.Version, actual.Shown.Heading, actual.Shown.ButtonLabel));
        Assert.Equal(expected.Shown.Lines, actual.Shown.Lines);
        Assert.Equal(expected.Origin, actual.Origin);
    }
}
