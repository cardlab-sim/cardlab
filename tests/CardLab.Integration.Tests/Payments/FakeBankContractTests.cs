using CardLab.Domain;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.FakeBank;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Contracts;

namespace CardLab.Integration.Tests.Payments;

// T-I-15 · PR-05 · integration (contract) — runs every CurrentAccountBankContractTests case against SqlFakeBank (AC-31).
// CreateBankAsync inserts a BankAccount row with the given id, last four and balance and returns a SqlFakeBank on a new context.
[Collection("sql")]
public sealed class FakeBankContractTests : CurrentAccountBankContractTests, IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _database;

    public FakeBankContractTests(SqlTestDatabase database) => _database = database;

    /// <inheritdoc />
    protected override async Task<ICurrentAccountBank> CreateBankAsync(BankAccountRef account, string lastFour, Money openingBalance)
    {
        await using (var setup = _database.CreateDbContext())
        {
            setup.BankAccounts.Add(new BankAccountRow { Id = account.Value, LastFour = lastFour, AvailableBalance = openingBalance.Amount });
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new SqlFakeBank(_database.CreateDbContext(), Clock);
    }
}
