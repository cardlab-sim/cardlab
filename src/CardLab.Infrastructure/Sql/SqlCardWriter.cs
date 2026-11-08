using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;

namespace CardLab.Infrastructure.Sql;

/// <summary>
/// <see cref="ICardWriter"/> over the DbContext inside <see cref="SqlCardStore.WithCardLockAsync{T}"/>: reads go
/// to the database (plus pending tracked changes for state), writes are tracked and saved on commit.
/// </summary>
/// <remarks>
/// Status and limit changes are queued and applied (in call order) the next time the writer reads or the unit of work
/// commits, because they need a query to find the existing row. Appends are tracked immediately and saved with them.
/// <c>OccurredAt</c> is stored to whole seconds, truncated (never rounded into the next period), matching <c>TransactionRules</c>.
/// </remarks>
internal sealed class SqlCardWriter : ICardWriter
{
    private readonly CardLabDbContext _db;
    private readonly CardId _cardId;
    private readonly List<Func<CancellationToken, Task>> _pending = [];
    private readonly SqlPaymentWriter _payments;

    /// <summary>Creates a writer for one locked card.</summary>
    /// <param name="db">The context whose transaction holds the lock.</param>
    /// <param name="cardId">The locked card.</param>
    public SqlCardWriter(CardLabDbContext db, CardId cardId)
    {
        _db = db;
        _cardId = cardId;
        _payments = new SqlPaymentWriter(db, cardId, FlushAsync);
    }

    /// <inheritdoc />
    public async Task<CardState?> GetStateAsync(CancellationToken ct = default)
    {
        await FlushAsync(ct);
        var card = await _db.Cards.AsNoTracking().SingleOrDefaultAsync(c => c.Id == _cardId.Value, ct);
        if (card is null)
        {
            return null;
        }

        var limits = await _db.SpendLimits.AsNoTracking().Where(l => l.CardId == _cardId.Value).ToListAsync(ct);
        return new CardState(
            new CardRef(_cardId, card.LastFour),
            Enum.Parse<CardStatus>(card.Status),
            limits.Select(RowMapper.ToLimit).ToList());
    }

    /// <inheritdoc />
    public async Task<Money> SumApprovedSpendAsync(Category category, PeriodRange range, CancellationToken ct = default)
    {
        await FlushAsync(ct);
        var categoryName = category.ToString();
        var approved = Outcome.Approved.ToString();
        var sum = await _db.Transactions
            .Where(t => t.CardId == _cardId.Value
                && t.Category == categoryName
                && t.Outcome == approved
                && t.OccurredAt >= range.StartUtc
                && t.OccurredAt < range.EndUtcExclusive)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        return RowMapper.ToMoney(sum);
    }

    /// <inheritdoc />
    public TransactionId AppendTransaction(NewTransaction transaction)
    {
        var row = RowMapper.ToRow(_cardId, transaction);
        _db.Transactions.Add(row);
        return new TransactionId(row.Id);
    }

    /// <inheritdoc />
    public void AppendAudit(AuditEvent auditEvent) =>
        _db.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid(),
            CardId = _cardId.Value,
            ActorId = auditEvent.Actor.Id,
            ActorName = auditEvent.Actor.Name,
            AtUtc = auditEvent.AtUtc.UtcDateTime,
            Action = auditEvent.Action.ToString(),
            OldValue = auditEvent.OldValue,
            NewValue = auditEvent.NewValue,
        });

    /// <inheritdoc />
    public void SetStatus(CardStatus status) =>
        _pending.Add(async ct =>
        {
            var card = await _db.Cards.SingleOrDefaultAsync(c => c.Id == _cardId.Value, ct);
            if (card is not null)
            {
                card.Status = status.ToString();
            }
        });

    /// <inheritdoc />
    public void UpsertLimit(SpendLimit limit) =>
        _pending.Add(async ct =>
        {
            var category = limit.Category.ToString();
            var period = limit.Period.ToString();
            var existing = await _db.SpendLimits.SingleOrDefaultAsync(
                l => l.CardId == _cardId.Value && l.Category == category && l.Period == period, ct);
            if (existing is null)
            {
                _db.SpendLimits.Add(new SpendLimitRow
                {
                    Id = Guid.NewGuid(),
                    CardId = _cardId.Value,
                    Category = category,
                    Period = period,
                    Amount = limit.Amount.Amount,
                });
            }
            else
            {
                existing.Amount = limit.Amount.Amount;
            }

            await _db.SaveChangesAsync(ct);
        });

    /// <inheritdoc />
    public void RemoveLimit(Category category, Period period) =>
        _pending.Add(async ct =>
        {
            var categoryName = category.ToString();
            var periodName = period.ToString();
            var existing = await _db.SpendLimits.SingleOrDefaultAsync(
                l => l.CardId == _cardId.Value && l.Category == categoryName && l.Period == periodName, ct);
            if (existing is not null)
            {
                _db.SpendLimits.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }
        });

    /// <inheritdoc />
    /// <remarks>One <c>SqlPaymentWriter</c> over the same context and card, created with this writer; its queued changes are
    /// applied after the 001 queued changes (in call order) at every flush.</remarks>
    public IPaymentWriter Payments => _payments;

    /// <summary>Applies queued changes and saves everything tracked (called before reads and on commit).</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the changes are written inside the open transaction.</returns>
    internal async Task FlushAsync(CancellationToken ct)
    {
        var queued = _pending.ToList();
        _pending.Clear();
        foreach (var apply in queued)
        {
            await apply(ct);
        }

        await _payments.ApplyPendingAsync(ct);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Domain ↔ row mapping (enums are stored by name; times as the column types require).</summary>
internal static class RowMapper
{
    /// <summary>Maps a decided transaction to a new row.</summary>
    /// <param name="cardId">Owning card.</param>
    /// <param name="t">The transaction.</param>
    /// <returns>A row with a fresh id.</returns>
    public static TransactionRow ToRow(CardId cardId, NewTransaction t) => new()
    {
        Id = Guid.NewGuid(),
        CardId = cardId.Value,
        Amount = t.Amount.Amount,
        Category = t.Category.ToString(),
        Merchant = t.Merchant,
        OccurredAt = t.OccurredAt.AddTicks(-(t.OccurredAt.Ticks % TimeSpan.TicksPerSecond)),
        DecidedAtUtc = t.DecidedAtUtc.UtcDateTime,
        Outcome = t.Outcome.ToString(),
        Reason = t.Reason.ToString(),
    };

    /// <summary>Maps a limit row to the domain limit.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The limit.</returns>
    public static SpendLimit ToLimit(SpendLimitRow row)
    {
        return new SpendLimit(Enum.Parse<Category>(row.Category), Enum.Parse<Period>(row.Period), ToMoney(row.Amount));
    }

    /// <summary>Converts a stored <c>decimal(12,2)</c> to money.</summary>
    /// <param name="value">The stored amount.</param>
    /// <returns>The money value.</returns>
    /// <exception cref="InvalidOperationException">The stored value is not a valid amount (the column definition changed).</exception>
    public static Money ToMoney(decimal value) =>
        Money.TryFromDecimal(value, out var money) ? money : throw new InvalidOperationException("A stored amount is not a valid money value.");

    /// <summary>Interprets a stored <c>datetime2</c> (always UTC) as an instant.</summary>
    /// <param name="utc">The stored value.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset ToInstant(DateTime utc) => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
