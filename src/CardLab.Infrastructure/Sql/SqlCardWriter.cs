using CardLab.Domain;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;

namespace CardLab.Infrastructure.Sql;

/// <summary>
/// <see cref="ICardWriter"/> over the DbContext inside <see cref="SqlCardStore.WithCardLockAsync{T}"/>: reads go
/// to the database (plus pending tracked changes for state), writes are tracked and saved on commit.
/// </summary>
/// <remarks>Implemented in PR-06.</remarks>
internal sealed class SqlCardWriter : ICardWriter
{
    private readonly CardLabDbContext _db;
    private readonly CardId _cardId;

    /// <summary>Creates a writer for one locked card.</summary>
    /// <param name="db">The context whose transaction holds the lock.</param>
    /// <param name="cardId">The locked card.</param>
    public SqlCardWriter(CardLabDbContext db, CardId cardId)
    {
        _db = db;
        _cardId = cardId;
    }

    /// <inheritdoc />
    public Task<CardState?> GetStateAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public Task<Money> SumApprovedSpendAsync(Category category, PeriodRange range, CancellationToken ct = default) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public TransactionId AppendTransaction(NewTransaction transaction) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public void AppendAudit(AuditEvent auditEvent) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public void SetStatus(CardStatus status) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public void UpsertLimit(SpendLimit limit) => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public void RemoveLimit(Category category, Period period) => throw new NotImplementedException("PR-06");
}
