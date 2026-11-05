using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Contracts;

namespace CardLab.Integration.Tests.Store;

// T-I-12 · PR-06 · integration (contract) — runs every CardStoreContractTests case against SqlCardStore on SQL Server.
// CreateStoreAsync inserts the given cards into the class database (unique ids/names) and returns a SqlCardStore on a new context.
[Collection("sql")]
public sealed class SqlCardStoreContractTests : CardStoreContractTests, IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public SqlCardStoreContractTests(SqlTestDatabase database) => _database = database;

    /// <inheritdoc />
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards) => throw new NotImplementedException("PR-06");

    /// <summary>PR-06 implements this: a new SqlCardStore on a new DbContext over the class database.</summary>
    /// <param name="seeded">A store returned by <see cref="CreateStoreAsync"/>.</param>
    /// <returns>A store over the same data.</returns>
    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) => throw new NotImplementedException("PR-06");
}
