using CardLab.Domain.Controls;
using CardLab.Infrastructure.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CardLab.Infrastructure.Sql;

/// <summary>Database reachability for <c>GET /api/health</c> (R-14, design §12).</summary>
/// <remarks>Implemented in PR-09.</remarks>
public sealed class DatabaseProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly CardLabDbContext _db;

    /// <summary>Creates the probe.</summary>
    /// <param name="db">Scoped context.</param>
    public DatabaseProbe(CardLabDbContext db) => _db = db;

    /// <summary>Runs <c>SELECT 1</c> with a 5 s timeout.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when the query succeeded; false on any connection or timeout error (never throws for those).</returns>
    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            _db.Database.SetCommandTimeout(ProbeTimeout);
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", timeout.Token);
            return true;
        }
        catch (Exception e) when (e is SqlException or InvalidOperationException || (e is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return false;
        }
    }
}

/// <summary>Test-only reset behind <c>POST /api/test/reset</c> (DEC-16).</summary>
/// <remarks>Implemented in PR-09.</remarks>
public sealed class TestDataReset
{
    private readonly CardLabDbContext _db;

    /// <summary>Creates the reset.</summary>
    /// <param name="db">Scoped context.</param>
    public TestDataReset(CardLabDbContext db) => _db = db;

    /// <summary>Deletes every transaction, audit event and limit and sets every card to Active, in one transaction.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the reset is committed.</returns>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.Transactions.ExecuteDeleteAsync(ct);
        await _db.AuditEvents.ExecuteDeleteAsync(ct);
        await _db.SpendLimits.ExecuteDeleteAsync(ct);
        var active = nameof(CardStatus.Active);
        await _db.Cards.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, active), ct);
        await transaction.CommitAsync(ct);
    }
}
