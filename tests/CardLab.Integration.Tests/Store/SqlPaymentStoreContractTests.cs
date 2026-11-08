using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
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

    // Given a quote and six events appended with one timestamp in one unit of work, When GetPaymentRecordAsync, Then the events come back in the order they were appended
    // Catches: random event ids ordering same-millisecond events arbitrarily, so a debit shows before its confirm (R-14, AC-20)
    [Fact]
    public async Task Events_with_equal_times_keep_call_order()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card], new LinkedAccountBuilder().WithId(Guid.NewGuid()).Build());
        var at = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var quote = new QuoteBuilder().ForCard(card).WithReference($"PAY-{Guid.NewGuid():N}"[..12]).At(at).Build();
        PaymentEventKind[] kinds =
        [
            PaymentEventKind.ConfirmCreated,
            PaymentEventKind.DebitRequested,
            PaymentEventKind.BankUnanswered,
            PaymentEventKind.StatusCheckUnanswered,
            PaymentEventKind.ConfirmRepeated,
            PaymentEventKind.StatusCheckCompleted,
        ];
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.Payments.AppendQuote(quote);
            foreach (var kind in kinds)
            {
                w.Payments.AppendEvent(new PaymentEvent(quote.Reference, null, at, kind, null, null, null, false));
            }

            return Task.FromResult(0);
        }, TestContext.Current.CancellationToken);

        var record = await store.GetPaymentRecordAsync(quote.Reference, TestContext.Current.CancellationToken);

        Assert.Equal(kinds, record!.Events.Select(e => e.Kind).ToArray());
    }
}
