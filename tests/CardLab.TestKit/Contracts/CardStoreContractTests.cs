using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;
using Xunit;

namespace CardLab.TestKit.Contracts;

/// <summary>A card to seed into a store under test (shape shared by the in-memory and SQL adapters).</summary>
/// <param name="Id">Card id.</param>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Lookup">Lookup hash of the card number.</param>
/// <param name="Status">Initial status.</param>
public sealed record StoreSeedCard(CardId Id, string Name, string CardholderName, string LastFour, LookupHash Lookup, CardStatus Status);

/// <summary>
/// The behaviour every <see cref="ICardStore"/> must have (T-U-21 / T-I-12). Subclasses supply a store seeded
/// with the given cards. Tests assert only on the cards they seeded themselves (a SQL database may also hold
/// other cards), filtering by id and checking relative order.
/// </summary>
public abstract class CardStoreContractTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a store containing (at least) the given cards, with no limits, transactions or audit for them.</summary>
    /// <param name="cards">Cards to seed.</param>
    /// <returns>The store.</returns>
    protected abstract Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards);

    /// <summary>Opens another handle on the same data, as a separate request would get (in-memory: the same instance; SQL: a new SqlCardStore on a new DbContext over the class database).</summary>
    /// <param name="seeded">A store returned by <see cref="CreateStoreAsync"/>.</param>
    /// <returns>A store over the same data that may be used concurrently with <paramref name="seeded"/>.</returns>
    protected abstract Task<ICardStore> OpenStoreAsync(ICardStore seeded);

    // T-U-21 / T-I-12 · PR-01 (written) · contract
    // Given two seeded cards, When ListCardsAsync, Then both are returned ordered by name with name, cardholder, last four and status
    // Catches: wrong ordering or a dropped/misnamed column in the adapter
    [Fact]
    public async Task Lists_seeded_cards_by_name()
    {
        var zulu = new CardBuilder().WithName("Zulu card").WithCardholder("Zed Holder").WithCardNumber("9999000900000017").Frozen().Build();
        var alpha = new CardBuilder().WithName("Alpha card").WithCardholder("Ann Holder").WithCardNumber("9999000900000025").Build();
        var store = await CreateStoreAsync([zulu, alpha]);

        var listed = (await store.ListCardsAsync()).Where(c => c.Id == zulu.Id || c.Id == alpha.Id).ToList();

        Assert.Equal(
            [
                new CardSummary(alpha.Id, "Alpha card", "Ann Holder", "0025", CardStatus.Active),
                new CardSummary(zulu.Id, "Zulu card", "Zed Holder", "0017", CardStatus.Frozen),
            ],
            listed);
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a seeded card, When GetCardAsync with its id / an unknown id, Then the detail (with limits) / null
    // Catches: unknown id throwing instead of returning null; limits missing from the detail
    [Fact]
    public async Task Gets_card_detail_or_null()
    {
        var card = new CardBuilder().WithName("Detail card").WithCardholder("Dee Holder").WithCardNumber("9999000900000033").Build();
        var store = await CreateStoreAsync([card]);
        var fuel = new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("50.00").Build();
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.UpsertLimit(fuel);
            return Task.FromResult(0);
        });

        var detail = await store.GetCardAsync(card.Id);
        var unknown = await store.GetCardAsync(new CardId(new Guid("0badc0de-0000-0000-0000-000000000001")));

        Assert.NotNull(detail);
        Assert.Equal(card.Id, detail.Id);
        Assert.Equal("Detail card", detail.Name);
        Assert.Equal("Dee Holder", detail.CardholderName);
        Assert.Equal("0033", detail.LastFour);
        Assert.Equal(CardStatus.Active, detail.Status);
        Assert.Equal([fuel], detail.Limits);
        Assert.Null(unknown);
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a seeded card, When FindByLookupAsync with its hash / another hash, Then its id / null
    // Catches: lookup comparing hashes by reference or case
    [Fact]
    public async Task Finds_card_by_lookup_hash()
    {
        var card = new CardBuilder().WithCardNumber("9999000900000041").Build();
        var store = await CreateStoreAsync([card]);
        var sameHashRebuilt = LookupHash.FromBytes(card.Lookup.ToBytes());
        var otherHash = new FakeCardNumberLookup().Compute("9999000900000058");

        Assert.Equal(card.Id, await store.FindByLookupAsync(sameHashRebuilt));
        Assert.Null(await store.FindByLookupAsync(otherHash));
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When work sets status, upserts and removes limits and appends a transaction and an audit event, Then all are visible after WithCardLockAsync returns
    // Catches: writes not flushed on commit
    [Fact]
    public async Task Commits_writes_when_work_returns()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);
        var fuel = new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("50.00").Build();
        var travel = new LimitBuilder().For(Category.Travel).Per(Period.Week).Of("20.00").Build();
        var transaction = new TransactionBuilder().Amount("12.34").In(Category.Fuel).Build();
        var audit = new AuditEvent(new CardRef(card.Id, card.LastFour), ActorBuilder.Sam, T0, AuditAction.Freeze, "active", "frozen");

        var statusSeenInside = await store.WithCardLockAsync(card.Id, async w =>
        {
            w.SetStatus(CardStatus.Frozen);
            w.UpsertLimit(fuel);
            w.UpsertLimit(travel);
            w.RemoveLimit(Category.Travel, Period.Week);
            w.AppendTransaction(transaction);
            w.AppendAudit(audit);
            return (await w.GetStateAsync())!.Status;
        });

        Assert.Equal(CardStatus.Frozen, statusSeenInside);
        var detail = await store.GetCardAsync(card.Id);
        Assert.Equal(CardStatus.Frozen, detail!.Status);
        Assert.Equal([fuel], detail.Limits);
        var transactions = await store.ListTransactionsAsync(card.Id);
        var stored = Assert.Single(transactions);
        Assert.Equal(("12.34", Category.Fuel, Outcome.Approved), (stored.Amount.ToString(), stored.Category, stored.Outcome));
        var auditRecords = await store.ListAuditAsync(card.Id);
        Assert.Equal(audit, Assert.Single(auditRecords).Event);
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When work writes and then throws, Then the exception propagates and nothing is persisted
    // Catches: partial writes surviving a failed unit of work (R-7 "nothing stored")
    [Fact]
    public async Task Discards_writes_when_work_throws()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);
        var audit = new AuditEvent(new CardRef(card.Id, card.LastFour), ActorBuilder.Sam, T0, AuditAction.Freeze, "active", "frozen");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WithCardLockAsync<int>(card.Id, w =>
        {
            w.SetStatus(CardStatus.Frozen);
            w.UpsertLimit(new LimitBuilder().Build());
            w.AppendTransaction(new TransactionBuilder().Build());
            w.AppendAudit(audit);
            throw new InvalidOperationException("work failed");
        }));

        var detail = await store.GetCardAsync(card.Id);
        Assert.Equal(CardStatus.Active, detail!.Status);
        Assert.Empty(detail.Limits);
        Assert.Empty(await store.ListTransactionsAsync(card.Id));
        Assert.Empty(await store.ListAuditAsync(card.Id));
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given no card with an id, When WithCardLockAsync(id) and GetStateAsync, Then the state is null
    // Catches: by-id paths unable to report card_not_found
    [Fact]
    public async Task State_is_null_for_unknown_card()
    {
        var store = await CreateStoreAsync([]);

        var state = await store.WithCardLockAsync(new CardId(new Guid("0badc0de-0000-0000-0000-000000000002")), w => w.GetStateAsync());

        Assert.Null(state);
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given approved and declined Fuel and Travel transactions inside, at the start of, at the end of and outside a range, When SumApprovedSpendAsync(Fuel, range), Then only approved Fuel with StartUtc <= OccurredAt < EndUtcExclusive count, including rows stored with a +01:00 offset
    // Catches: declines counted (R-5), inclusive end, offset-sensitive comparison
    [Fact]
    public async Task Sums_only_approved_spend_of_category_in_half_open_range()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);
        var start = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 4, 11, 0, 0, TimeSpan.Zero);
        var range = new PeriodRange(start, end, new DateOnly(2026, 11, 4));

        // Amounts are distinct powers of two so the expected sum identifies exactly which rows counted.
        var rows = new[]
        {
            new TransactionBuilder().Amount("1.00").In(Category.Fuel).At(start).Build(),                                  // at the start: counts
            new TransactionBuilder().Amount("2.00").In(Category.Fuel).At(start.AddMinutes(30)).Build(),                    // inside: counts
            new TransactionBuilder().Amount("4.00").In(Category.Fuel).At(new DateTimeOffset(2026, 11, 4, 11, 30, 0, TimeSpan.FromHours(1))).Build(), // 10:30Z in +01:00: counts
            new TransactionBuilder().Amount("8.00").In(Category.Fuel).At(end).Build(),                                     // at the end: excluded
            new TransactionBuilder().Amount("16.00").In(Category.Fuel).At(new DateTimeOffset(2026, 11, 4, 11, 0, 0, TimeSpan.FromHours(1))).Build(), // 10:00Z in +01:00 = the start: counts
            new TransactionBuilder().Amount("32.00").In(Category.Fuel).At(start.AddSeconds(-1)).Build(),                   // before: excluded
            new TransactionBuilder().Amount("64.00").In(Category.Fuel).At(end.AddSeconds(1)).Build(),                      // after: excluded
            new TransactionBuilder().Amount("128.00").In(Category.Fuel).At(start.AddMinutes(10)).Declined(Reason.LimitExceeded).Build(), // declined: excluded
            new TransactionBuilder().Amount("256.00").In(Category.Travel).At(start.AddMinutes(10)).Build(),                // other category: excluded
        };

        await store.WithCardLockAsync(card.Id, w =>
        {
            foreach (var row in rows)
            {
                w.AppendTransaction(row);
            }

            return Task.FromResult(0);
        });
        var sum = await store.WithCardLockAsync(card.Id, w => w.SumApprovedSpendAsync(Category.Fuel, range));

        Assert.Equal("23.00", sum.ToString());
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When UpsertLimit(Fuel/Day 50.00) then UpsertLimit(Fuel/Day 80.00) in separate units of work, Then exactly one Fuel/Day limit of 80.00 exists
    // Catches: duplicate limit rows (R-4 "at most one")
    [Fact]
    public async Task Upsert_replaces_limit_for_same_category_and_period()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);

        await store.WithCardLockAsync(card.Id, w =>
        {
            w.UpsertLimit(new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("50.00").Build());
            return Task.FromResult(0);
        });
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.UpsertLimit(new LimitBuilder().For(Category.Fuel).Per(Period.Day).Of("80.00").Build());
            return Task.FromResult(0);
        });

        var detail = await store.GetCardAsync(card.Id);
        var only = Assert.Single(detail!.Limits);
        Assert.Equal((Category.Fuel, Period.Day, "80.00"), (only.Category, only.Period, only.Amount.ToString()));
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given transactions and audit events appended with increasing DecidedAtUtc / AtUtc, When listed, Then newest first, transaction ids match AppendTransaction's return value, audit records carry the card's last four
    // Catches: oldest-first history (AC-22, AC-23), ids not round-tripping
    [Fact]
    public async Task Lists_history_newest_first()
    {
        var card = new CardBuilder().WithCardNumber("9999000900000066").Build();
        var store = await CreateStoreAsync([card]);
        var cardRef = new CardRef(card.Id, card.LastFour);

        // Appended out of time order on purpose, so only a real sort by time gives newest first.
        var ids = await store.WithCardLockAsync(card.Id, w => Task.FromResult(new[]
        {
            w.AppendTransaction(new TransactionBuilder().Amount("2.00").DecidedAt(T0.AddMinutes(2)).Build()),
            w.AppendTransaction(new TransactionBuilder().Amount("3.00").DecidedAt(T0.AddMinutes(3)).Build()),
            w.AppendTransaction(new TransactionBuilder().Amount("1.00").DecidedAt(T0.AddMinutes(1)).Build()),
        }));
        await store.WithCardLockAsync(card.Id, w =>
        {
            w.AppendAudit(new AuditEvent(cardRef, ActorBuilder.Sam, T0.AddMinutes(2), AuditAction.Unfreeze, "frozen", "active"));
            w.AppendAudit(new AuditEvent(cardRef, ActorBuilder.Sam, T0.AddMinutes(3), AuditAction.LimitSet, null, "fuel/day 50.00"));
            w.AppendAudit(new AuditEvent(cardRef, ActorBuilder.Sam, T0.AddMinutes(1), AuditAction.Freeze, "active", "frozen"));
            return Task.FromResult(0);
        });

        var transactions = await store.ListTransactionsAsync(card.Id);
        var audit = await store.ListAuditAsync(card.Id);

        Assert.Equal([ids[1], ids[0], ids[2]], transactions.Select(t => t.Id));
        Assert.Equal(["3.00", "2.00", "1.00"], transactions.Select(t => t.Amount.ToString()));
        Assert.All(transactions, t => Assert.Equal(card.Id, t.CardId));
        Assert.Equal([AuditAction.LimitSet, AuditAction.Unfreeze, AuditAction.Freeze], audit.Select(a => a.Event.Action));
        Assert.All(audit, a => Assert.Equal("0066", a.Event.Card.LastFour));
        Assert.All(audit, a => Assert.Equal(card.Id, a.Event.Card.Id));
    }

    // T-U-21 / T-I-12 · PR-06 review M-2 · contract
    // Given a card, When one unit of work calls SetStatus, reads the state, then calls SetStatus twice more, Then the unit completes and the last status is stored
    // Catches: a writer that cannot set the status more than once per unit of work (adapter drift from the fake)
    [Fact]
    public async Task Sets_status_repeatedly_in_one_unit_of_work()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);

        var seen = await store.WithCardLockAsync(card.Id, async w =>
        {
            w.SetStatus(CardStatus.Frozen);
            var midway = (await w.GetStateAsync())!.Status;
            w.SetStatus(CardStatus.Active);
            w.SetStatus(CardStatus.Frozen);
            return midway;
        });

        Assert.Equal(CardStatus.Frozen, seen);
        Assert.Equal(CardStatus.Frozen, (await store.GetCardAsync(card.Id))!.Status);
    }

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When 10 concurrent WithCardLockAsync calls each read the transaction count and append one transaction, Then every work saw a distinct count 0..9 (serialised) and 10 rows exist
    // Catches: missing per-card lock (R-12, DEC-4)
    [Fact]
    public async Task Serialises_work_on_the_same_card()
    {
        var card = new CardBuilder().Build();
        var store = await CreateStoreAsync([card]);
        var wholeDay = new PeriodRange(T0.AddDays(-1), T0.AddDays(1), new DateOnly(2026, 11, 4));

        var stores = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => OpenStoreAsync(store)));

        var seen = await Task.WhenAll(stores.Select(s => s.WithCardLockAsync(card.Id, async w =>
        {
            // Every transaction is GBP 1.00, so the approved sum is the transaction count.
            var count = (int)(await w.SumApprovedSpendAsync(Category.Fuel, wholeDay)).Amount;
            await Task.Yield();
            w.AppendTransaction(new TransactionBuilder().Amount("1.00").In(Category.Fuel).At(T0).Build());
            return count;
        })));

        Assert.Equal(Enumerable.Range(0, 10), seen.Order());
        Assert.Equal(10, (await store.ListTransactionsAsync(card.Id)).Count);
    }
}
