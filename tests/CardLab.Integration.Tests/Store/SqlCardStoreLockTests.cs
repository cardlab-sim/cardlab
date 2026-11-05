using System.Diagnostics;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
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
        var holder = new SqlCardStore(_database.CreateDbContext());
        var waiter = new SqlCardStore(_database.CreateDbContext());
        var other = new SqlCardStore(_database.CreateDbContext());
        var lockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Releasing the holder through a gate (instead of a 7 s sleep) keeps the lock held for exactly as long as the assertions need.
        var holding = holder.WithCardLockAsync(opsVan, async _ =>
        {
            lockHeld.SetResult();
            await release.Task;
            return 0;
        }, ct);
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
        release.SetResult();
        await holding;

        Assert.InRange(started.Elapsed, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(12));
        Assert.Contains(opsVan.Value.ToString(), error.Message);
        var ops = await new SqlCardStore(_database.CreateDbContext()).GetCardAsync(opsVan, ct);
        Assert.Equal(CardStatus.Active, ops!.Status);
        Assert.Empty(ops.Limits);
        var field = await new SqlCardStore(_database.CreateDbContext()).GetCardAsync(fieldSales, ct);
        Assert.Equal(CardStatus.Frozen, field!.Status);
    }
}
