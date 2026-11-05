using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
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
    protected override async Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards)
    {
        await using (var db = _database.CreateDbContext())
        {
            // Card names are unique per account and every default-built card is "Test card", so each call gets a fresh
            // account and the cards keep exactly the names they were seeded with.
            var account = new BusinessAccountRow { Id = Guid.NewGuid(), Name = $"Contract {Guid.NewGuid():N}", TimeZoneId = "Europe/London" };
            db.BusinessAccounts.Add(account);
            db.Cards.AddRange(cards.Select(c => new CardRow
            {
                Id = c.Id.Value,
                AccountId = account.Id,
                Name = c.Name,
                CardholderName = c.CardholderName,
                LastFour = c.LastFour,
                LookupHash = c.Lookup.ToBytes(),
                Status = c.Status.ToString(),
            }));
            await db.SaveChangesAsync();
        }

        return new SqlCardStore(_database.CreateDbContext());
    }

    /// <inheritdoc />
    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) =>
        Task.FromResult<ICardStore>(new SqlCardStore(_database.CreateDbContext()));
}
