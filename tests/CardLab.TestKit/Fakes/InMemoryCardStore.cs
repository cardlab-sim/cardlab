using System.Collections.Concurrent;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
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
/// Ids assigned by <see cref="ICardWriter.AppendTransaction"/> and <see cref="ICardWriter.AppendAudit"/> come from a
/// counter (deterministic, never random). Sums see committed rows; do not rely on sums including rows appended in
/// the same unit of work (SQL does not). The lock wait is capped at 5 s like the SQL adapter and then throws
/// <see cref="CardLockTimeoutException"/>.
/// </remarks>
public sealed class InMemoryCardStore : ICardStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly Dictionary<CardId, CardData> cards = [];
    private readonly ConcurrentDictionary<CardId, SemaphoreSlim> locks = new();
    private long idCounter;

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

        public CardData Copy() => new(this);
    }

    private sealed class Writer(InMemoryCardStore store, CardData? card) : ICardWriter
    {
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
}
