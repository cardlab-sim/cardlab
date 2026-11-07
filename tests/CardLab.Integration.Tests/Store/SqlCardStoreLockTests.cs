using System.Diagnostics;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Store;

[Collection("sql")]
public sealed class SqlCardStoreLockTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public SqlCardStoreLockTests(SqlTestDatabase database) => _database = database;

    // T-I-17 · PR-06 · integration
    // Given one unit of work holding Ops van's lock for 7 s, When a second WithCardLockAsync on Ops van starts, Then it throws CardLockTimeoutException after about 5 s and its writes are not stored; a unit of work on Field sales meanwhile completes immediately
    // Catches: unbounded lock waits, lock on the wrong resource, or a global lock (DEC-4, design §9)
    [Fact]
    public async Task Lock_times_out_after_five_seconds_per_card_only()
    {
        var ct = TestContext.Current.CancellationToken;
        var opsVan = await _database.CardIdAsync(TestSeed.OpsVan.Name);
        var fieldSales = await _database.CardIdAsync(TestSeed.FieldSales.Name);
        await using var holderDb = _database.CreateDbContext();
        await using var waiterDb = _database.CreateDbContext();
        await using var otherDb = _database.CreateDbContext();
        await using var readDb = _database.CreateDbContext();
        var holder = new SqlCardStore(holderDb);
        var waiter = new SqlCardStore(waiterDb);
        var other = new SqlCardStore(otherDb);
        var lockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Releasing the holder through a gate (instead of a 7 s sleep) keeps the lock held for exactly as long as the assertions need.
        var holding = holder.WithCardLockAsync(opsVan, async _ =>
        {
            lockHeld.SetResult();
            await release.Task;
            return 0;
        }, ct);
        try
        {
            await lockHeld.Task;

            var started = Stopwatch.StartNew();
            var blocked = waiter.WithCardLockAsync(opsVan, w =>
            {
                w.SetStatus(CardStatus.Frozen);
                w.UpsertLimit(new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("10.00").Build());
                return Task.FromResult(0);
            }, ct);

            // Another card is not blocked while Ops van's lock is held and the waiter is still waiting.
            await other.WithCardLockAsync(fieldSales, w =>
            {
                w.SetStatus(CardStatus.Frozen);
                return Task.FromResult(0);
            }, ct);
            Assert.False(blocked.IsCompleted);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(3));

            var error = await Assert.ThrowsAsync<CardLockTimeoutException>(() => blocked);
            started.Stop();

            Assert.InRange(started.Elapsed, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(12));
            Assert.Contains(opsVan.Value.ToString(), error.Message);
        }
        finally
        {
            release.TrySetResult();
            await holding;
        }

        var reader = new SqlCardStore(readDb);
        var ops = await reader.GetCardAsync(opsVan, ct);
        Assert.Equal(CardStatus.Active, ops!.Status);
        Assert.Empty(ops.Limits);
        var field = await reader.GetCardAsync(fieldSales, ct);
        Assert.Equal(CardStatus.Frozen, field!.Status);
    }

    // T-I-12 (SQL-specific) · PR-06 review M-1
    // Given the database connection dies inside a unit of work, When the pending writes are flushed, Then the caller gets the SqlException (not a masked InvalidOperationException), so the API can answer 503
    // Catches: a catch block that rolls back a dead transaction and hides the real failure (design §9)
    [Fact]
    public async Task Dropped_connection_surfaces_the_sql_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var marketing = await _database.CardIdAsync(TestSeed.Marketing.Name);
        await using var db = _database.CreateDbContext();
        var store = new SqlCardStore(db);

        await Assert.ThrowsAsync<SqlException>(() => store.WithCardLockAsync(marketing, async w =>
        {
            var spid = await db.Database.SqlQueryRaw<short>("SELECT @@SPID AS Value").ToListAsync(ct);
            await KillSessionAsync(spid[0], ct);
            w.SetStatus(CardStatus.Frozen);
            return 0;
        }, ct));
    }

    // T-I-12 (SQL-specific) · PR-06 review m-3
    // Given a unit of work that writes, reads (which flushes the writes) and then throws, When it propagates, Then nothing is stored
    // Catches: committing instead of rolling back after a flushed failure (R-7 "nothing stored")
    [Fact]
    public async Task Failure_after_a_flushing_read_stores_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var opsVan = await _database.CardIdAsync(TestSeed.OpsVan.Name);
        await using var db = _database.CreateDbContext();
        await using var readDb = _database.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqlCardStore(db).WithCardLockAsync<int>(opsVan, async w =>
        {
            w.SetStatus(CardStatus.Frozen);
            w.UpsertLimit(new LimitBuilder().Build());
            w.AppendTransaction(new TransactionBuilder().Build());
            await w.GetStateAsync(ct);
            throw new InvalidOperationException("work failed");
        }, ct));

        var reader = new SqlCardStore(readDb);
        var ops = await reader.GetCardAsync(opsVan, ct);
        Assert.Equal(CardStatus.Active, ops!.Status);
        Assert.Empty(ops.Limits);
        Assert.Empty(await reader.ListTransactionsAsync(opsVan, ct));
    }

    // T-I-12 (SQL-specific) · PR-06 review m-4
    // Given a transaction occurring at 10:59:59.600Z, When the spend for [10:00Z, 11:00Z) is summed, Then it counts (the stored instant is truncated to whole seconds, never rounded into the next period)
    // Catches: datetimeoffset(0) rounding a spend in the last half-second into the next period (R-5, R-12)
    [Fact]
    public async Task Occurred_at_is_truncated_to_whole_seconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var marketing = await _database.CardIdAsync(TestSeed.Marketing.Name);
        await using var db = _database.CreateDbContext();
        var store = new SqlCardStore(db);
        var hour = new PeriodRange(
            new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 4, 11, 0, 0, TimeSpan.Zero),
            new DateOnly(2026, 11, 4));

        var sum = await store.WithCardLockAsync(marketing, async w =>
        {
            w.AppendTransaction(new TransactionBuilder().Amount("1.00").In(Category.Fuel)
                .At(new DateTimeOffset(2026, 11, 4, 10, 59, 59, 600, TimeSpan.Zero)).Build());
            return await w.SumApprovedSpendAsync(Category.Fuel, hour, ct);
        }, ct);

        Assert.Equal("1.00", sum.ToString());
    }

    private async Task KillSessionAsync(short spid, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(_database.ConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"KILL {spid}";
        await command.ExecuteNonQueryAsync(ct);
    }
}
