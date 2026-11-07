using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class FakeBankPersistenceTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _fixture;

    public FakeBankPersistenceTests(SqlTestDatabase fixture) => _fixture = fixture;

    // T-I-12 · PR-05 · integration
    // Given a completed £150.50 debit through one SqlFakeBank, When a new DbContext/SqlFakeBank is created and Seeder.EnsureSeedAsync runs again (a restart), Then the entry is still listed and the available balance is still £24,849.50
    // Catches: the ledger or balance reset by a restart or re-seed (AC-22, R-15)
    [Fact(Skip = "todo")]
    public void Ledger_survives_restart_and_reseed()
    {
    }

    // T-I-22 · PR-05 · integration
    // Given a fresh database, When Seeder.EnsureSeedAsync runs twice, Then exactly one LinkedAccount (name, last four 7731, BankAccountRef, authority basis) and one BankAccount (£25,000.00) exist and Database.DumpAllRowsAsync contains no "51907731"
    // Catches: duplicate seeds or the full account number stored (R-2, NFR-3)
    [Fact(Skip = "todo")]
    public void Seeds_linked_account_once_without_the_full_number()
    {
    }
}
