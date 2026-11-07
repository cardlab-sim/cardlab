using System.Data;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Payments;
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
/// <see cref="SqlCardWriter"/>; <c>SaveChanges</c> + commit (releases the lock); an uncommitted transaction is rolled back on dispose.
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
        return new CardDetail(id, card.Name, card.CardholderName, card.LastFour, Enum.Parse<CardStatus>(card.Status), limits.Select(RowMapper.ToLimit).OrderBy(l => l.Category).ThenBy(l => l.Period).ToList());
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
        // No catch: an uncommitted transaction is rolled back on dispose, so a dead connection's SqlException
        // reaches the caller instead of a masked InvalidOperationException from a rollback attempt.
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

    /// <inheritdoc />
    /// <remarks>Reads <c>LinkedAccount</c>; the system has one per business (R-2), so the first by id is the account.</remarks>
    public async Task<LinkedAccount?> GetLinkedAccountAsync(CancellationToken ct = default)
    {
        var row = await _db.LinkedAccounts.AsNoTracking().OrderBy(l => l.Id).FirstOrDefaultAsync(ct);
        return row is null ? null : new LinkedAccount(row.Id, row.DisplayName, row.LastFour, new BankAccountRef(row.BankAccountRef), row.AuthorityBasis);
    }

    /// <inheritdoc />
    /// <remarks>Two indexed SUMs (<c>IX_Transaction_Spend</c> approved rows; <c>IX_Payment_Completed</c>); owed is <c>BalanceRules.Owed</c> of the two.</remarks>
    public async Task<CardBalance?> GetBalanceAsync(CardId id, CancellationToken ct = default)
    {
        if (!await _db.Cards.AsNoTracking().AnyAsync(c => c.Id == id.Value, ct))
        {
            return null;
        }

        var approved = Outcome.Approved.ToString();
        var purchases = RowMapper.ToMoney(await _db.Transactions.AsNoTracking()
            .Where(t => t.CardId == id.Value && t.Outcome == approved)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m);
        var payments = RowMapper.ToMoney(await PaymentMapping.SumCompletedAsync(_db, id, ct));
        return new CardBalance(purchases, payments, BalanceRules.Owed(purchases, payments));
    }

    /// <inheritdoc />
    public async Task<PaymentRecord?> GetPaymentRecordAsync(PaymentReference reference, CancellationToken ct = default)
    {
        var quote = await _db.PaymentQuotes.AsNoTracking().SingleOrDefaultAsync(q => q.Reference == reference.Value, ct);
        if (quote is null)
        {
            return null;
        }

        var payment = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Reference == reference.Value, ct);
        var events = await _db.PaymentEvents.AsNoTracking().Where(e => e.Reference == reference.Value).OrderBy(e => e.AtUtc).ThenBy(e => e.Id).ToListAsync(ct);
        return ToRecord(quote, payment, events);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentRecord>> ListPaymentRecordsAsync(CardId id, CancellationToken ct = default)
    {
        var quotes = await _db.PaymentQuotes.AsNoTracking().Where(q => q.CardId == id.Value).OrderByDescending(q => q.CreatedAtUtc).ToListAsync(ct);
        var payments = (await _db.Payments.AsNoTracking().Where(p => p.CardId == id.Value).ToListAsync(ct)).ToDictionary(p => p.Reference);
        var events = (await (
            from e in _db.PaymentEvents.AsNoTracking()
            join q in _db.PaymentQuotes on e.Reference equals q.Reference
            where q.CardId == id.Value
            orderby e.AtUtc, e.Id
            select e).ToListAsync(ct)).ToLookup(e => e.Reference);
        return quotes.Select(q => ToRecord(q, payments.GetValueOrDefault(q.Reference), events[q.Reference].ToList())).ToList();
    }

    /// <inheritdoc />
    /// <remarks>Purchases and payments are read separately (never the <c>Bank*</c> tables, DEC-2), each limited to
    /// <paramref name="take"/> newest rows by decision time (purchases) or confirm time (payments), then merged newest first
    /// by that time. A purchase is shown at its processor timestamp (<c>OccurredAt</c>, 001 AC-38; BUG-009).</remarks>
    public async Task<IReadOnlyList<ActivityEntry>> ListActivityAsync(CardId id, int take, CancellationToken ct = default)
    {
        var purchases = await _db.Transactions.AsNoTracking()
            .Where(t => t.CardId == id.Value)
            .OrderByDescending(t => t.DecidedAtUtc)
            .Take(take)
            .ToListAsync(ct);
        var payments = await (
            from p in _db.Payments.AsNoTracking()
            where p.CardId == id.Value
            join q in _db.PaymentQuotes on p.Reference equals q.Reference
            orderby p.CreatedAtUtc descending
            select new { Payment = p, q.Origin }).Take(take).ToListAsync(ct);
        var entries = purchases.Select(t => (
                Placed: RowMapper.ToInstant(t.DecidedAtUtc),
                Entry: new ActivityEntry(
                    t.OccurredAt,
                    ActivityKind.Purchase,
                    RowMapper.ToMoney(t.Amount),
                    t.Merchant,
                    Enum.Parse<Outcome>(t.Outcome),
                    Enum.Parse<Reason>(t.Reason),
                    Enum.Parse<Category>(t.Category),
                    null,
                    null,
                    null)))
            .Concat(payments.Select(x => (
                Placed: RowMapper.ToInstant(x.Payment.CreatedAtUtc),
                Entry: new ActivityEntry(
                    RowMapper.ToInstant(x.Payment.CreatedAtUtc),
                    ActivityKind.Payment,
                    RowMapper.ToMoney(x.Payment.Amount),
                    $"Payment {x.Payment.Reference}",
                    null,
                    null,
                    null,
                    Enum.Parse<PaymentState>(x.Payment.State),
                    new PaymentReference(x.Payment.Reference),
                    Enum.Parse<PaymentOrigin>(x.Origin)))));
        return entries.OrderByDescending(e => e.Placed).Take(take).Select(e => e.Entry).ToList();
    }

    private static PaymentRecord ToRecord(PaymentQuoteRow quote, PaymentRow? payment, IReadOnlyList<PaymentEventRow> events) =>
        new(
            PaymentMapping.ToQuote(quote),
            Enum.Parse<QuoteStatus>(quote.Status),
            quote.RefusalReason,
            quote.BalanceAtRefusal is { } balance ? RowMapper.ToMoney(balance) : null,
            payment is null ? null : PaymentMapping.ToSnapshot(payment),
            events.Select(PaymentMapping.ToEvent).ToList());

    private static TransactionRecord ToRecord(TransactionRow t)
    {
        var amount = RowMapper.ToMoney(t.Amount);
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
