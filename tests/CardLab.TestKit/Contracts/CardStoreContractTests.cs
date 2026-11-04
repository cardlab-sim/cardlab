using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using Xunit;

namespace CardLab.TestKit.Contracts;

/// <summary>A card to put into a store under test (both adapters can seed it).</summary>
/// <param name="Id">Card id.</param>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="LastFour">Last four digits.</param>
/// <param name="Lookup">Lookup hash of its number.</param>
/// <param name="Status">Initial status.</param>
public sealed record StoreSeedCard(CardId Id, string Name, string CardholderName, string LastFour, LookupHash Lookup, CardStatus Status);

/// <summary>
/// The <see cref="ICardStore"/>/<see cref="ICardWriter"/> contract, run against <c>InMemoryCardStore</c>
/// (T-U-21, <c>tests/CardLab.Domain.Tests/Fakes/InMemoryCardStoreContractTests.cs</c>) and <c>SqlCardStore</c>
/// (T-I-12, <c>tests/CardLab.Integration.Tests/Store/SqlCardStoreContractTests.cs</c>), so the fake cannot drift.
/// Bodies written in PR-01 (against the in-memory store); PR-06 makes them pass against SQL without editing this file.
/// </summary>
public abstract class CardStoreContractTests
{
    /// <summary>Creates an empty store (fresh per test) seeded with the given cards.</summary>
    /// <param name="cards">Cards to seed (use <c>CardBuilder</c>).</param>
    /// <returns>The store under test.</returns>
    protected abstract Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards);

    // T-U-21 / T-I-12 · PR-01 (written) · contract
    // Given two seeded cards, When ListCardsAsync, Then both are returned ordered by name with name, cardholder, last four and status
    // Catches: wrong ordering or a dropped/misnamed column in the adapter
    /// <summary>Contract C-1.</summary>
    [Fact(Skip = "todo")]
    public Task Lists_seeded_cards_by_name() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a seeded card, When GetCardAsync with its id / an unknown id, Then the detail (with limits) / null
    // Catches: unknown id throwing instead of returning null; limits missing from the detail
    /// <summary>Contract C-2.</summary>
    [Fact(Skip = "todo")]
    public Task Gets_card_detail_or_null() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a seeded card, When FindByLookupAsync with its hash / another hash, Then its id / null
    // Catches: lookup comparing hashes by reference or case
    /// <summary>Contract C-3.</summary>
    [Fact(Skip = "todo")]
    public Task Finds_card_by_lookup_hash() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When work sets status, upserts and removes limits and appends a transaction and an audit event, Then all are visible after WithCardLockAsync returns
    // Catches: writes not flushed on commit
    /// <summary>Contract C-4.</summary>
    [Fact(Skip = "todo")]
    public Task Commits_writes_when_work_returns() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When work writes and then throws, Then the exception propagates and nothing is persisted
    // Catches: partial writes surviving a failed unit of work (R-7 "nothing stored")
    /// <summary>Contract C-5.</summary>
    [Fact(Skip = "todo")]
    public Task Discards_writes_when_work_throws() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given no card with an id, When WithCardLockAsync(id) and GetStateAsync, Then the state is null
    // Catches: by-id paths unable to report card_not_found
    /// <summary>Contract C-6.</summary>
    [Fact(Skip = "todo")]
    public Task State_is_null_for_unknown_card() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given approved and declined Fuel and Travel transactions inside, at the start of, at the end of and outside a range, When SumApprovedSpendAsync(Fuel, range), Then only approved Fuel with StartUtc <= OccurredAt < EndUtcExclusive count, including rows stored with a +01:00 offset
    // Catches: declines counted (R-5), inclusive end, offset-sensitive comparison
    /// <summary>Contract C-7.</summary>
    [Fact(Skip = "todo")]
    public Task Sums_only_approved_spend_of_category_in_half_open_range() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When UpsertLimit(Fuel/Day 50.00) then UpsertLimit(Fuel/Day 80.00) in separate units of work, Then exactly one Fuel/Day limit of 80.00 exists
    // Catches: duplicate limit rows (R-4 "at most one")
    /// <summary>Contract C-8.</summary>
    [Fact(Skip = "todo")]
    public Task Upsert_replaces_limit_for_same_category_and_period() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given transactions and audit events appended with increasing DecidedAtUtc / AtUtc, When listed, Then newest first, transaction ids match AppendTransaction's return value, audit records carry the card's last four
    // Catches: oldest-first history (AC-22, AC-23), ids not round-tripping
    /// <summary>Contract C-9.</summary>
    [Fact(Skip = "todo")]
    public Task Lists_history_newest_first() => Task.CompletedTask;

    // T-U-21 / T-I-12 · PR-01 · contract
    // Given a card, When 10 concurrent WithCardLockAsync calls each read the transaction count and append one transaction, Then every work saw a distinct count 0..9 (serialised) and 10 rows exist
    // Catches: missing per-card lock (R-12, DEC-4)
    /// <summary>Contract C-10.</summary>
    [Fact(Skip = "todo")]
    public Task Serialises_work_on_the_same_card() => Task.CompletedTask;
}
