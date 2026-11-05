using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Seed;

[Collection("sql")]
public sealed class SeederTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SeederTests(ApiFactory factory) => _factory = factory;

    // T-I-13 · PR-06 · integration
    // Given the host started (migrate + seed ran) and Seeder.EnsureSeedAsync is run twice more, When the tables are read, Then exactly 1 account "Brightline Supplies Ltd" (Europe/London), 1 admin "Sam Ellis" with Seed.AdminId, 3 cards with the seeded names, cardholders, last four 0001/0002/0003, Active, and ICardStore.FindByLookupAsync(lookup of each seeded number) returns that card
    // Catches: duplicate rows on redeploy, missing seed, hash computed with a different key/format
    [Fact]
    public async Task Seed_is_idempotent_and_cards_are_findable_by_number()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.Services; // starts the host: migrate + seed
        await ReseedAsync(ct);
        await ReseedAsync(ct);

        await using var db = _factory.Database.CreateDbContext();
        var account = await db.BusinessAccounts.SingleAsync(ct);
        var admin = await db.AdminUsers.SingleAsync(ct);
        var cards = await db.Cards.OrderBy(c => c.Name).ToListAsync(ct);
        Assert.Equal(("Brightline Supplies Ltd", "Europe/London"), (account.Name, account.TimeZoneId));
        Assert.Equal((TestSeed.AdminId, "Sam Ellis", account.Id), (admin.Id, admin.DisplayName, admin.AccountId));
        Assert.Equal(
            [
                ("Field sales", "Tom Okafor", "0002", "Active"),
                ("Marketing", "Leah Brooks", "0003", "Active"),
                ("Ops van", "Priya Nair", "0001", "Active"),
            ],
            cards.Select(c => (c.Name, c.CardholderName, c.LastFour, c.Status)));

        using var scope = _factory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ICardNumberLookup>();
        var store = scope.ServiceProvider.GetRequiredService<ICardStore>();
        foreach (var seeded in TestSeed.Cards)
        {
            var found = await store.FindByLookupAsync(lookup.Compute(seeded.CardNumber), ct);
            Assert.Equal(cards.Single(c => c.Name == seeded.Name).Id, found!.Value.Value);
        }
    }

    // T-I-13 · PR-06 · integration
    // Given a card whose LookupHash was overwritten with zeros and which is Frozen with a limit, When EnsureSeedAsync runs, Then the hash is recomputed and status, limits and history are untouched
    // Catches: key rotation not healed (DEC-8), or reseed wiping state
    [Fact]
    public async Task Reseed_recomputes_lookup_hash_and_keeps_state()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.Services;
        var database = _factory.Database;
        var name = TestSeed.Marketing.Name;
        var cardId = await database.CardIdAsync(name);
        await database.SetStatusAsync(name, CardStatus.Frozen);
        await database.SeedLimitAsync(name, Category.Fuel, Period.Day, 25.00m);
        await database.SeedTransactionsAsync(name, [new TransactionBuilder().Amount("5.00").Build()]);
        await using (var db = database.CreateDbContext())
        {
            await db.Cards.Where(c => c.Id == cardId.Value).ExecuteUpdateAsync(s => s.SetProperty(c => c.LookupHash, new byte[32]), ct);
        }

        try
        {
            await ReseedAsync(ct);

            using var scope = _factory.Services.CreateScope();
            var expected = scope.ServiceProvider.GetRequiredService<ICardNumberLookup>().Compute(TestSeed.Marketing.CardNumber);
            await using var db = database.CreateDbContext();
            var card = await db.Cards.SingleAsync(c => c.Id == cardId.Value, ct);
            Assert.Equal(expected, LookupHash.FromBytes(card.LookupHash));
            Assert.Equal("Frozen", card.Status);
            var limit = await db.SpendLimits.SingleAsync(l => l.CardId == cardId.Value, ct);
            Assert.Equal(25.00m, limit.Amount);
            Assert.Equal(1, await db.Transactions.CountAsync(t => t.CardId == cardId.Value, ct));
        }
        finally
        {
            await database.SetStatusAsync(name, CardStatus.Active);
        }
    }

    private async Task ReseedAsync(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        await Seeder.EnsureSeedAsync(scope.ServiceProvider, options, ct);
    }
}
