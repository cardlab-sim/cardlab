using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.FakeBank;
using CardLab.Infrastructure.Lookup;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class FakeBankPersistenceTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _fixture;

    public FakeBankPersistenceTests(SqlTestDatabase fixture) => _fixture = fixture;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-12 · PR-05 · integration
    // Given a completed £150.50 debit through one SqlFakeBank, When a new DbContext/SqlFakeBank is created and Seeder.EnsureSeedAsync runs again (a restart), Then the entry is still listed and the available balance is still £24,849.50
    // Catches: the ledger or balance reset by a restart or re-seed (AC-22, R-15)
    [Fact]
    public async Task Ledger_survives_restart_and_reseed()
    {
        await ReseedAsync();
        var account = new BankAccountRef(TestSeed.BankAccountRef);
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero));
        var reference = new PaymentReference("PAY-PERSIST1");
        await using (var db = _fixture.CreateDbContext())
        {
            var first = new SqlFakeBank(db, clock);
            var outcome = await first.RequestDebitAsync(new DebitRequest(account, reference, TestMoney.Of("150.50"), "CardLab card payment PAY-PERSIST1"), Ct);
            Assert.IsType<BankOutcome.Completed>(outcome);
        }

        await ReseedAsync();
        await using var restarted = _fixture.CreateDbContext();
        var bank = new SqlFakeBank(restarted, clock);

        var snapshot = await bank.GetAccountAsync(account, Ct);
        var entries = await bank.ListRecentEntriesAsync(account, 10, Ct);

        Assert.Equal("24849.50", snapshot.AvailableBalance.ToString());
        var entry = Assert.Single(entries);
        Assert.Equal(("PAY-PERSIST1", "150.50"), (entry.Reference.Value, entry.Amount.ToString()));
    }

    // T-I-22 · PR-05 · integration
    // Given a fresh database, When Seeder.EnsureSeedAsync runs twice, Then exactly one LinkedAccount (name, last four 7731, BankAccountRef, authority basis) and one BankAccount (£25,000.00) exist and Database.DumpAllRowsAsync contains no "51907731"
    // Catches: duplicate seeds or the full account number stored (R-2, NFR-3)
    [Fact]
    public async Task Seeds_linked_account_once_without_the_full_number()
    {
        await ClearBankAndLinkedAccountAsync();
        await ReseedAsync();
        await ReseedAsync();

        await using var db = _fixture.CreateDbContext();
        var linked = await db.LinkedAccounts.AsNoTracking().SingleAsync(Ct);
        var bank = await db.BankAccounts.AsNoTracking().SingleAsync(Ct);
        var steering = await db.BankSteerings.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((TestSeed.LinkedAccountName, "7731", TestSeed.BankAccountRef, TestSeed.AuthorityBasis), (linked.DisplayName, linked.LastFour, linked.BankAccountRef, linked.AuthorityBasis));
        Assert.Equal((TestSeed.BankAccountRef, "7731", 25000.00m), (bank.Id, bank.LastFour, bank.AvailableBalance));
        Assert.Equal((1, null, null), (steering.Id, steering.NextDebitOutcome, steering.NextStatusCheck));
        Assert.DoesNotContain(TestSeed.LinkedAccountNumber, await _fixture.DumpAllRowsAsync(), StringComparison.Ordinal);
    }

    // Given a linked-account section with a number shorter than four digits, When Seeder.EnsureSeedAsync runs, Then it throws InvalidOperationException and writes no LinkedAccount
    // Catches: a typo in the configured number silently shipping an app with no linked account (R-2)
    [Fact]
    public async Task Invalid_linked_account_number_fails_the_seed_loudly()
    {
        await ClearBankAndLinkedAccountAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReseedAsync("773"));

        await using var db = _fixture.CreateDbContext();
        Assert.Empty(await db.LinkedAccounts.AsNoTracking().ToListAsync(Ct));
    }

    // Starts the seed tests from empty seed tables so they never depend on what ran before them in this class.
    private async Task ClearBankAndLinkedAccountAsync()
    {
        await using var db = _fixture.CreateDbContext();
        await db.BankEntries.ExecuteDeleteAsync(Ct);
        await db.BankDebitRequests.ExecuteDeleteAsync(Ct);
        await db.BankAccounts.ExecuteDeleteAsync(Ct);
        await db.BankSteerings.ExecuteDeleteAsync(Ct);
        await db.LinkedAccounts.ExecuteDeleteAsync(Ct);
    }

    // Runs the seed again through a fresh scope, as an application restart does.
    private async Task ReseedAsync(string? accountNumber = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CardLabDbContext>(o => o.UseSqlServer(_fixture.ConnectionString));
        services.Configure<LookupOptions>(o => o.LookupKey = TestSeed.DevLookupKey);
        services.AddSingleton<ICardNumberLookup, HmacCardNumberLookup>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var options = new SeedOptions
        {
            AccountName = TestSeed.AccountName,
            TimeZoneId = TestSeed.TimeZoneId,
            AdminId = TestSeed.AdminId,
            AdminName = TestSeed.AdminName,
            Cards = TestSeed.Cards.Select(c => new SeedCardOptions { Name = c.Name, CardholderName = c.CardholderName, CardNumber = c.CardNumber }).ToList(),
            LinkedAccount = new SeedLinkedAccountOptions
            {
                DisplayName = TestSeed.LinkedAccountName,
                AccountNumber = accountNumber ?? TestSeed.LinkedAccountNumber,
                BankAccountRef = TestSeed.BankAccountRef,
                OpeningBalance = TestSeed.BankOpeningBalance,
                AuthorityBasis = TestSeed.AuthorityBasis,
            },
        };
        await Seeder.EnsureSeedAsync(scope.ServiceProvider, options);
    }
}
