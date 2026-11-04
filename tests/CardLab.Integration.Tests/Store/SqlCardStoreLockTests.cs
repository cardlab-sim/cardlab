using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Store;

[Collection("sql")]
public sealed class SqlCardStoreLockTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public SqlCardStoreLockTests(SqlTestDatabase database) => _database = database;

    // T-I-17 · PR-06 · integration
    // Given one unit of work holding Ops van's lock for 7 s, When a second WithCardLockAsync on Ops van starts, Then it throws CardLockTimeoutException after about 5 s and its writes are not stored; a unit of work on Field sales meanwhile completes immediately
    // Catches: unbounded lock waits, lock on the wrong resource, or a global lock (DEC-4, design §9)
    [Fact(Skip = "todo")]
    public void Lock_times_out_after_five_seconds_per_card_only()
    {
    }
}
