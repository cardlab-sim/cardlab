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
            var accountId = db.BusinessAccounts.Select(a => a.Id).First();
            var taken = db.Cards.Select(c => c.Name).ToHashSet();
            db.Cards.AddRange(cards.Select(c => new CardRow
            {
                Id = c.Id.Value,
                AccountId = accountId,

                // Names are unique per account; every default-built card is "Test card", so repeats get their id appended.
                Name = taken.Add(c.Name) ? c.Name : $"{c.Name} {c.Id.Value:N}",
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
