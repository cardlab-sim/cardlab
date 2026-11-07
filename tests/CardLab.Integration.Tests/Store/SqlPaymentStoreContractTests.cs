using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Contracts;

namespace CardLab.Integration.Tests.Store;

// T-I-16 · PR-05 · integration (contract) — runs every PaymentStoreContractTests case against SqlCardStore on SQL Server.
// CreateStoreAsync inserts a fresh business account, the cards and a LinkedAccount row for that account, and returns a SqlCardStore on a new context.
[Collection("sql")]
public sealed class SqlPaymentStoreContractTests : PaymentStoreContractTests, IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public SqlPaymentStoreContractTests(SqlTestDatabase database) => _database = database;

    /// <inheritdoc />
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards, LinkedAccount account) =>
        throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) => throw new NotImplementedException("PR-05");
}
