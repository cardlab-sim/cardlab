using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.TestKit.Fakes;

/// <summary>
/// In-memory <see cref="ICardStore"/> for unit tests, with a per-card <c>SemaphoreSlim</c> as the lock and
/// copy-on-commit semantics (writes are applied only when the work returns; discarded when it throws). Must pass
/// <see cref="CardStoreContractTests"/> (T-U-21) so it cannot drift from <c>SqlCardStore</c>. Implemented in PR-01.
/// </summary>
public sealed class InMemoryCardStore : ICardStore
{
    /// <summary>Creates a store holding the given cards (no limits, transactions or audit).</summary>
    /// <param name="cards">Cards to seed, e.g. from <c>CardBuilder</c>.</param>
    public InMemoryCardStore(params IEnumerable<StoreSeedCard> cards) => throw new NotImplementedException("PR-01");

    /// <summary>Total transactions across all cards ("nothing stored anywhere", AC-19/AC-20).</summary>
    public int TransactionCount => throw new NotImplementedException("PR-01");

    /// <summary>Total audit events across all cards.</summary>
    public int AuditCount => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<IReadOnlyList<CardSummary>> ListCardsAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<IReadOnlyList<TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-01");
}
