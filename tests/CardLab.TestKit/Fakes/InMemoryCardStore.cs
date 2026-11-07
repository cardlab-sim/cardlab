using System.Collections.Concurrent;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Payments;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.TestKit.Fakes;

/// <summary>
/// In-memory <see cref="ICardStore"/> for unit tests, with a per-card <c>SemaphoreSlim</c> as the lock and
/// copy-on-commit semantics (writes are applied only when the work returns; discarded when it throws). Must pass
/// <see cref="CardStoreContractTests"/> (T-U-21) so it cannot drift from <c>SqlCardStore</c>. Implemented in PR-01.
/// </summary>
/// <remarks>
/// 002 (PR-02): also implements the payment reads and <see cref="ICardWriter.Payments"/> with the same copy-on-commit
/// semantics, enforcing on commit like the SQL constraints: one pending payment per card, one payment per reference,
/// one quote per reference across cards, and a payment needs a quote; must pass <c>PaymentStoreContractTests</c> (T-U-23).
/// Ids assigned by <see cref="ICardWriter.AppendTransaction"/> and <see cref="ICardWriter.AppendAudit"/> come from a
/// counter (deterministic, never random). Payment reads and sums inside a unit of work see that unit's own writes
/// here; SQL does not. Never rely on it. The lock wait is capped at 5 s like the SQL adapter and then throws
/// <see cref="CardLockTimeoutException"/>.
/// </remarks>
public sealed class InMemoryCardStore : ICardStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly Dictionary<CardId, CardData> cards = [];
    private readonly ConcurrentDictionary<CardId, SemaphoreSlim> locks = new();
    private long idCounter;
    private LinkedAccount? linkedAccount;

    /// <summary>Creates a store holding the given cards (no limits, transactions or audit).</summary>
    /// <param name="cards">Cards to seed, e.g. from <c>CardBuilder</c>.</param>
    public InMemoryCardStore(params IEnumerable<StoreSeedCard> cards)
    {
        foreach (var card in cards)
        {
            this.cards[card.Id] = new CardData(card);
        }
    }

    /// <summary>Total transactions across all cards ("nothing stored anywhere", AC-19/AC-20).</summary>
    public int TransactionCount
    {
        get
        {
            lock (gate)
            {
                return cards.Values.Sum(c => c.Transactions.Count);
            }
        }
    }

    /// <summary>Total audit events across all cards.</summary>
    public int AuditCount
    {
        get
        {
            lock (gate)
            {
                return cards.Values.Sum(c => c.Audit.Count);
            }
        }
    }

    /// <summary>002: payments across all cards (any state).</summary>
    public int PaymentCount
    {
        get
        {
            lock (gate)
            {
                return cards.Values.Sum(c => c.Payments.Count);
            }
        }
    }

    /// <summary>002: quotes (references) across all cards.</summary>
    public int QuoteCount
    {
        get
        {
            lock (gate)
            {
                return cards.Values.Sum(c => c.Quotes.Count);
            }
        }
    }

    /// <summary>002: sets the linked account returned by <see cref="GetLinkedAccountAsync"/> (none by default).</summary>
    /// <param name="account">The account, e.g. from <c>LinkedAccountBuilder</c>.</param>
    /// <returns>This store.</returns>
    public InMemoryCardStore WithLinkedAccount(LinkedAccount account)
    {
        lock (gate)
        {
            linkedAccount = account;
        }

        return this;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CardSummary>> ListCardsAsync(CancellationToken ct = default)
    {
        lock (gate)
        {
            IReadOnlyList<CardSummary> result = cards.Values
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .Select(c => new CardSummary(c.Id, c.Name, c.CardholderName, c.LastFour, c.Status))
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default)
    {
        lock (gate)
        {
            var detail = cards.TryGetValue(id, out var c)
                ? new CardDetail(c.Id, c.Name, c.CardholderName, c.LastFour, c.Status, c.Limits.OrderBy(l => l.Category).ThenBy(l => l.Period).ToList())
                : null;
            return Task.FromResult(detail);
        }
    }

    /// <inheritdoc />
    public Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default)
    {
        lock (gate)
        {
            var match = cards.Values.FirstOrDefault(c => c.Lookup == hash);
            return Task.FromResult(match is null ? (CardId?)null : match.Id);
        }
    }

    /// <inheritdoc />
    public async Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default)
    {
        var semaphore = locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(LockTimeout, ct))
        {
            throw new CardLockTimeoutException("The card lock was not granted within 5 seconds.");
        }

        try
        {
            CardData? working;
            lock (gate)
            {
                working = cards.TryGetValue(id, out var committed) ? committed.Copy() : null;
            }

            var result = await work(new Writer(this, working));
            if (working is not null)
            {
                lock (gate)
                {
                    EnforcePaymentUniqueness(working);
                    cards[id] = working;
                }
            }

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default)
    {
        lock (gate)
        {
            IReadOnlyList<TransactionRecord> result = cards.TryGetValue(id, out var c)
                ? Enumerable.Reverse(c.Transactions).OrderByDescending(t => t.DecidedAtUtc).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default)
    {
        lock (gate)
        {
            IReadOnlyList<AuditRecord> result = cards.TryGetValue(id, out var c)
                ? Enumerable.Reverse(c.Audit).OrderByDescending(a => a.Event.AtUtc).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<LinkedAccount?> GetLinkedAccountAsync(CancellationToken ct = default)
    {
        lock (gate)
        {
            return Task.FromResult(linkedAccount);
        }
    }

    /// <inheritdoc />
    public Task<CardBalance?> GetBalanceAsync(CardId id, CancellationToken ct = default)
    {
        lock (gate)
        {
            if (!cards.TryGetValue(id, out var c))
            {
                return Task.FromResult<CardBalance?>(null);
            }

            var purchases = c.ApprovedPurchases();
            var completed = c.CompletedPayments();
            return Task.FromResult<CardBalance?>(new CardBalance(purchases, completed, purchases - completed));
        }
    }

    /// <inheritdoc />
    public Task<PaymentRecord?> GetPaymentRecordAsync(PaymentReference reference, CancellationToken ct = default)
    {
        lock (gate)
        {
            var record = cards.Values
                .Select(c => c.Quotes.Any(q => q.Quote.Reference == reference) ? c.RecordOf(reference) : null)
                .FirstOrDefault(r => r is not null);
            return Task.FromResult(record);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PaymentRecord>> ListPaymentRecordsAsync(CardId id, CancellationToken ct = default)
    {
        lock (gate)
        {
            IReadOnlyList<PaymentRecord> result = cards.TryGetValue(id, out var c)
                ? Enumerable.Reverse(c.Quotes).OrderByDescending(q => q.Quote.CreatedAtUtc).Select(q => c.RecordOf(q.Quote.Reference)).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActivityEntry>> ListActivityAsync(CardId id, int take, CancellationToken ct = default)
    {
        lock (gate)
        {
            if (!cards.TryGetValue(id, out var c))
            {
                return Task.FromResult<IReadOnlyList<ActivityEntry>>([]);
            }

            var purchases = c.Transactions.Select(t => (Placed: t.DecidedAtUtc, Entry: new ActivityEntry(
                t.OccurredAt, ActivityKind.Purchase, t.Amount, t.Merchant, t.Outcome, t.Reason, t.Category, null, null, null)));
            var payments = c.Payments.Select(p => (Placed: p.CreatedAtUtc, Entry: new ActivityEntry(
                p.CreatedAtUtc,
                ActivityKind.Payment,
                p.Amount,
                $"Payment {p.Reference}",
                null,
                null,
                null,
                p.State,
                p.Reference,
                c.Quotes.First(q => q.Quote.Reference == p.Reference).Quote.Origin)));
            IReadOnlyList<ActivityEntry> merged = Enumerable.Reverse(purchases.Concat(payments).ToList())
                .OrderByDescending(a => a.Placed).Take(take).Select(a => a.Entry).ToList();
            return Task.FromResult(merged);
        }
    }

    // Mirrors the SQL constraints (PaymentQuote primary key, Payment.Reference foreign key and unique index,
    // UX_Payment_OnePending): checked on commit, so a violating unit of work stores nothing. Caller holds the gate.
    private void EnforcePaymentUniqueness(CardData working)
    {
        foreach (var stored in working.Quotes)
        {
            var duplicated = working.Quotes.Count(q => q.Quote.Reference == stored.Quote.Reference) > 1
                || cards.Values.Any(c => c.Id != working.Id && c.Quotes.Any(q => q.Quote.Reference == stored.Quote.Reference));
            if (duplicated)
            {
                throw new InvalidOperationException("A reference may have only one quote.");
            }
        }

        if (working.Payments.Any(p => working.Quotes.All(q => q.Quote.Reference != p.Reference)))
        {
            throw new InvalidOperationException("A payment needs a quote for its reference.");
        }

        if (working.Payments.Count(p => p.State == PaymentState.Pending) > 1)
        {
            throw new InvalidOperationException("A card may have only one pending payment.");
        }

        foreach (var payment in working.Payments)
        {
            var duplicated = working.Payments.Count(p => p.Reference == payment.Reference) > 1
                || cards.Values.Any(c => c.Id != working.Id && c.Payments.Any(p => p.Reference == payment.Reference));
            if (duplicated)
            {
                throw new InvalidOperationException("A reference may have only one payment.");
            }
        }
    }

    private Guid NextId(byte marker)
    {
        var n = Interlocked.Increment(ref idCounter);
        return new Guid((int)n, 0, 0, marker, marker, marker, marker, marker, marker, marker, marker);
    }

    private sealed class CardData
    {
        public CardData(StoreSeedCard seed)
        {
            Id = seed.Id;
            Name = seed.Name;
            CardholderName = seed.CardholderName;
            LastFour = seed.LastFour;
            Lookup = seed.Lookup;
            Status = seed.Status;
        }

        private CardData(CardData other)
        {
            Id = other.Id;
            Name = other.Name;
            CardholderName = other.CardholderName;
            LastFour = other.LastFour;
            Lookup = other.Lookup;
            Status = other.Status;
            Limits = [.. other.Limits];
            Transactions = [.. other.Transactions];
            Audit = [.. other.Audit];
            Quotes = [.. other.Quotes];
            Payments = [.. other.Payments];
            Events = [.. other.Events];
        }

        public CardId Id { get; }

        public string Name { get; }

        public string CardholderName { get; }

        public string LastFour { get; }

        public LookupHash Lookup { get; }

        public CardStatus Status { get; set; }

        public List<SpendLimit> Limits { get; } = [];

        public List<TransactionRecord> Transactions { get; } = [];

        public List<AuditRecord> Audit { get; } = [];

        public List<StoredQuote> Quotes { get; } = [];

        public List<PaymentSnapshot> Payments { get; } = [];

        public List<PaymentEvent> Events { get; } = [];

        public Money ApprovedPurchases() =>
            Transactions.Where(t => t.Outcome == Outcome.Approved).Aggregate(Money.Zero, (sum, t) => sum + t.Amount);

        public Money CompletedPayments() =>
            Payments.Where(p => p.State == PaymentState.Completed).Aggregate(Money.Zero, (sum, p) => sum + p.Amount);

        public PaymentRecord RecordOf(PaymentReference reference)
        {
            var stored = Quotes.First(q => q.Quote.Reference == reference);
            return new PaymentRecord(
                stored.Quote,
                stored.Status,
                stored.RefusalReason,
                stored.BalanceAtRefusal,
                Payments.FirstOrDefault(p => p.Reference == reference),
                Events.Where(e => e.Reference == reference).ToList());
        }

        public CardData Copy() => new(this);
    }

    private sealed record StoredQuote(Quote Quote, QuoteStatus Status, string? RefusalReason, Money? BalanceAtRefusal);

    private sealed class Writer(InMemoryCardStore store, CardData? card) : ICardWriter
    {
        public IPaymentWriter Payments => new PaymentWriter(store, Existing());

        public Task<CardState?> GetStateAsync(CancellationToken ct = default) =>
            Task.FromResult(card is null ? null : new CardState(new CardRef(card.Id, card.LastFour), card.Status, card.Limits.ToList()));

        public Task<Money> SumApprovedSpendAsync(Category category, PeriodRange range, CancellationToken ct = default)
        {
            var sum = Money.Zero;
            foreach (var t in Existing().Transactions)
            {
                if (t.Outcome == Outcome.Approved
                    && t.Category == category
                    && t.OccurredAt >= range.StartUtc
                    && t.OccurredAt < range.EndUtcExclusive)
                {
                    sum += t.Amount;
                }
            }

            return Task.FromResult(sum);
        }

        public TransactionId AppendTransaction(NewTransaction transaction)
        {
            var target = Existing();
            var id = new TransactionId(store.NextId(0x7A));
            target.Transactions.Add(new TransactionRecord(
                id,
                target.Id,
                transaction.Amount,
                transaction.Category,
                transaction.Merchant,
                transaction.OccurredAt,
                transaction.DecidedAtUtc,
                transaction.Outcome,
                transaction.Reason));
            return id;
        }

        public void AppendAudit(AuditEvent auditEvent) =>
            Existing().Audit.Add(new AuditRecord(new AuditEventId(store.NextId(0xAD)), auditEvent));

        public void SetStatus(CardStatus status) => Existing().Status = status;

        public void UpsertLimit(SpendLimit limit)
        {
            var limits = Existing().Limits;
            var index = limits.FindIndex(l => l.Category == limit.Category && l.Period == limit.Period);
            if (index >= 0)
            {
                limits[index] = limit;
            }
            else
            {
                limits.Add(limit);
            }
        }

        public void RemoveLimit(Category category, Period period) =>
            Existing().Limits.RemoveAll(l => l.Category == category && l.Period == period);

        private CardData Existing() =>
            card ?? throw new InvalidOperationException("No card has the locked id; only GetStateAsync is meaningful.");
    }

    private sealed class PaymentWriter(InMemoryCardStore store, CardData card) : IPaymentWriter
    {
        public Task<Money> SumApprovedPurchasesAsync(CancellationToken ct = default) => Task.FromResult(card.ApprovedPurchases());

        public Task<Money> SumCompletedPaymentsAsync(CancellationToken ct = default) => Task.FromResult(card.CompletedPayments());

        public Task<QuoteRecord?> GetQuoteAsync(PaymentReference reference, CancellationToken ct = default)
        {
            var stored = card.Quotes.FirstOrDefault(q => q.Quote.Reference == reference);
            return Task.FromResult(stored is null
                ? null
                : new QuoteRecord(stored.Quote, stored.Status, card.Payments.FirstOrDefault(p => p.Reference == reference)));
        }

        public Task<PaymentSnapshot?> GetPendingPaymentAsync(CancellationToken ct = default) =>
            Task.FromResult(card.Payments.FirstOrDefault(p => p.State == PaymentState.Pending));

        public void AppendQuote(Quote quote) => card.Quotes.Add(new StoredQuote(quote, QuoteStatus.Open, null, null));

        public void SetQuoteStatus(PaymentReference reference, QuoteStatus status, string? refusalReason, Money? balanceAtRefusal, DateTimeOffset atUtc)
        {
            var index = card.Quotes.FindIndex(q => q.Quote.Reference == reference);
            card.Quotes[index] = card.Quotes[index] with { Status = status, RefusalReason = refusalReason, BalanceAtRefusal = balanceAtRefusal };
        }

        public PaymentId CreatePayment(NewPayment payment)
        {
            var id = new PaymentId(store.NextId(0x9A));
            card.Payments.Add(new PaymentSnapshot(id, payment.Reference, PaymentState.Pending, payment.Amount, payment.CreatedAtUtc, null));
            return id;
        }

        public void SettlePayment(PaymentId id, Settlement settlement)
        {
            var index = card.Payments.FindIndex(p => p.Id == id);
            card.Payments[index] = card.Payments[index] with { State = settlement.State, Settlement = settlement };
        }

        public void AppendEvent(PaymentEvent paymentEvent) => card.Events.Add(paymentEvent);
    }
}
