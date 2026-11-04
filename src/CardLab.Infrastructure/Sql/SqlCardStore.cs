using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;

namespace CardLab.Infrastructure.Sql;

/// <summary>
/// SQL Server implementation of <see cref="ICardStore"/> (EF Core). Must pass <c>CardStoreContractTests</c> (T-I-12).
/// </summary>
/// <remarks>
/// <c>WithCardLockAsync</c> (DEC-4): begin a transaction; <c>EXEC @r = sp_getapplock @Resource = 'card:{id}',
/// @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000</c> with the resource passed as a
/// parameter; <c>@r &lt; 0</c> → roll back and throw <see cref="CardLockTimeoutException"/>; run the work through a
/// <see cref="SqlCardWriter"/>; <c>SaveChanges</c> + commit (releases the lock); roll back if the work throws.
/// Spend = <c>SUM(Amount)</c> of approved rows with <c>OccurredAt</c> in the range. Enum columns hold enum names.
/// Implemented in PR-06.
/// </remarks>
public sealed class SqlCardStore : ICardStore
{
    private readonly CardLabDbContext _db;

    /// <summary>Creates the store over a scoped DbContext.</summary>
    /// <param name="db">Scoped context.</param>
    public SqlCardStore(CardLabDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<IReadOnlyList<CardSummary>> ListCardsAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<IReadOnlyList<TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default) => throw new NotImplementedException("PR-06");
}
