using CardLab.Infrastructure.Model;

namespace CardLab.Infrastructure.Sql;

/// <summary>Database reachability for <c>GET /api/health</c> (R-14, design §12).</summary>
/// <remarks>Implemented in PR-09.</remarks>
public sealed class DatabaseProbe
{
    private readonly CardLabDbContext _db;

    /// <summary>Creates the probe.</summary>
    /// <param name="db">Scoped context.</param>
    public DatabaseProbe(CardLabDbContext db) => _db = db;

    /// <summary>Runs <c>SELECT 1</c> with a 5 s timeout.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when the query succeeded; false on any connection or timeout error (never throws for those).</returns>
    public Task<bool> CanConnectAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-09");
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
    public Task ResetAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-09");
}
