using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
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
    protected override async Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards, LinkedAccount account)
    {
        await using (var db = _database.CreateDbContext())
        {
            // A fresh business account per call keeps card names unique and gives the account its own LinkedAccount row.
            var business = new BusinessAccountRow { Id = Guid.NewGuid(), Name = $"Contract {Guid.NewGuid():N}", TimeZoneId = "Europe/London" };
            db.BusinessAccounts.Add(business);
            db.LinkedAccounts.Add(new LinkedAccountRow
            {
                Id = account.Id,
                AccountId = business.Id,
                DisplayName = account.DisplayName,
                LastFour = account.LastFour,
                BankAccountRef = account.BankRef.Value,
                AuthorityBasis = account.AuthorityBasis,
            });
            // Default-built cards share one name and names are unique per account, so later duplicates get their position appended.
            db.Cards.AddRange(cards.Select((c, i) => new CardRow
            {
                Id = c.Id.Value,
                AccountId = business.Id,
                Name = cards.Take(i).Any(earlier => earlier.Name == c.Name) ? $"{c.Name} {i + 1}" : c.Name,
                CardholderName = c.CardholderName,
                LastFour = c.LastFour,
                LookupHash = c.Lookup.ToBytes(),
                Status = c.Status.ToString(),
            }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new SqlCardStore(_database.CreateDbContext());
    }

    /// <inheritdoc />
    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) =>
        Task.FromResult<ICardStore>(new SqlCardStore(_database.CreateDbContext()));
}
