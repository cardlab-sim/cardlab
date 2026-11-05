using System.Data;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
/// One instance serves one unit of work at a time (it shares its scoped DbContext).
/// </remarks>
public sealed class SqlCardStore : ICardStore
{
    private readonly CardLabDbContext _db;

    /// <summary>Creates the store over a scoped DbContext.</summary>
    /// <param name="db">Scoped context.</param>
    public SqlCardStore(CardLabDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardSummary>> ListCardsAsync(CancellationToken ct = default)
    {
        var rows = await _db.Cards.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return rows.Select(c => new CardSummary(new CardId(c.Id), c.Name, c.CardholderName, c.LastFour, Enum.Parse<CardStatus>(c.Status))).ToList();
    }

    /// <inheritdoc />
    public async Task<CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default)
    {
        var card = await _db.Cards.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id.Value, ct);
        if (card is null)
        {
            return null;
        }

        var limits = await _db.SpendLimits.AsNoTracking().Where(l => l.CardId == id.Value).ToListAsync(ct);
        return new CardDetail(id, card.Name, card.CardholderName, card.LastFour, Enum.Parse<CardStatus>(card.Status), limits.Select(RowMapper.ToLimit).ToList());
    }

    /// <inheritdoc />
    public async Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default)
    {
        var bytes = hash.ToBytes();
        var id = await _db.Cards.AsNoTracking().Where(c => c.LookupHash == bytes).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct);
        return id is null ? null : new CardId(id.Value);
    }

    /// <inheritdoc />
    public async Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = new SqlParameter("@r", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"card:{id.Value}" };
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                [result, resource],
                ct);
            if ((int)result.Value < 0)
            {
                throw new CardLockTimeoutException($"The lock for card {id.Value} was not granted within 5 seconds.");
            }

            var writer = new SqlCardWriter(_db, id);
            var value = await work(writer);
            await writer.FlushAsync(ct);
            await tx.CommitAsync(ct);
            return value;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default)
    {
        var rows = await _db.Transactions.AsNoTracking()
            .Where(t => t.CardId == id.Value)
            .OrderByDescending(t => t.DecidedAtUtc)
            .ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default)
    {
        var lastFour = await _db.Cards.AsNoTracking().Where(c => c.Id == id.Value).Select(c => c.LastFour).SingleOrDefaultAsync(ct);
        var rows = await _db.AuditEvents.AsNoTracking()
            .Where(a => a.CardId == id.Value)
            .OrderByDescending(a => a.AtUtc)
            .ToListAsync(ct);
        return rows.Select(a => new AuditRecord(
            new AuditEventId(a.Id),
            new AuditEvent(
                new CardRef(id, lastFour ?? ""),
                new Actor(a.ActorId, a.ActorName),
                RowMapper.ToInstant(a.AtUtc),
                Enum.Parse<AuditAction>(a.Action),
                a.OldValue,
                a.NewValue))).ToList();
    }

    private static TransactionRecord ToRecord(TransactionRow t)
    {
        Money.TryFromDecimal(t.Amount, out var amount);
        return new TransactionRecord(
            new TransactionId(t.Id),
            new CardId(t.CardId),
            amount,
            Enum.Parse<Category>(t.Category),
            t.Merchant,
            t.OccurredAt,
            RowMapper.ToInstant(t.DecidedAtUtc),
            Enum.Parse<Outcome>(t.Outcome),
            Enum.Parse<Reason>(t.Reason));
    }
}
