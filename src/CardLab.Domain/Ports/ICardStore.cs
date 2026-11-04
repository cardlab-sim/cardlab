using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;

namespace CardLab.Domain.Ports;

/// <summary>
/// Persistence port for cards, limits, transactions and audit events (design §5). Implemented by
/// <c>SqlCardStore</c> (PR-06) and <c>InMemoryCardStore</c> (PR-01); both must pass
/// <c>CardStoreContractTests</c>.
/// </summary>
public interface ICardStore
{
    /// <summary>Lists every card of the (single) business account, ordered by name.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>All cards.</returns>
    Task<IReadOnlyList<CardSummary>> ListCardsAsync(CancellationToken ct = default);

    /// <summary>Reads one card with its limits.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The card, or null when no card has this id.</returns>
    Task<CardDetail?> GetCardAsync(CardId id, CancellationToken ct = default);

    /// <summary>Finds the card whose number has this lookup hash.</summary>
    /// <param name="hash">Lookup hash of the submitted card number.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The card id, or null for an unknown number.</returns>
    Task<CardId?> FindByLookupAsync(LookupHash hash, CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> as one atomic unit of work holding an exclusive per-card lock, so all
    /// writes to one card (authorisations and control changes) are serialised (R-12, DEC-4).
    /// </summary>
    /// <remarks>
    /// Writes made through the <see cref="ICardWriter"/> are committed when <paramref name="work"/> returns
    /// and discarded when it throws. Different cards do not block each other. The lock is taken even when
    /// no card has this id (the writer then reports a null state).
    /// </remarks>
    /// <typeparam name="T">Result of the work.</typeparam>
    /// <param name="id">Card to lock.</param>
    /// <param name="work">Reads and writes through the writer; must not escape the writer.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>What <paramref name="work"/> returned.</returns>
    /// <exception cref="CardLockTimeoutException">The lock was not granted within 5 s; nothing stored.</exception>
    Task<T> WithCardLockAsync<T>(CardId id, Func<ICardWriter, Task<T>> work, CancellationToken ct = default);

    /// <summary>Transaction history of a card, newest first by <c>DecidedAtUtc</c> (ties: unspecified order).</summary>
    /// <param name="id">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>All transactions of the card; empty for an unknown id.</returns>
    Task<IReadOnlyList<TransactionRecord>> ListTransactionsAsync(CardId id, CancellationToken ct = default);

    /// <summary>Audit events of a card, newest first by <c>AtUtc</c> (ties: unspecified order) (R-6).</summary>
    /// <param name="id">Card id.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>All audit events of the card; empty for an unknown id.</returns>
    Task<IReadOnlyList<AuditRecord>> ListAuditAsync(CardId id, CancellationToken ct = default);
}

/// <summary>Unit of work on one card, valid only inside <see cref="ICardStore.WithCardLockAsync{T}"/>.</summary>
public interface ICardWriter
{
    /// <summary>Reads the card's status and limits (including writes already made in this unit of work).</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The state, or null when no card has the locked id (PLANNER-DECISION: nullable so by-id paths can report card not found).</returns>
    Task<CardState?> GetStateAsync(CancellationToken ct = default);

    /// <summary>Sums the amounts of approved transactions of a category with <c>OccurredAt</c> in <c>[range.StartUtc, range.EndUtcExclusive)</c>.</summary>
    /// <param name="category">Category.</param>
    /// <param name="range">Period range (compared by instant, whatever the stored offset).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The sum; £0.00 when none. Declined transactions never count (R-5).</returns>
    Task<Money> SumApprovedSpendAsync(Category category, PeriodRange range, CancellationToken ct = default);

    /// <summary>Appends a transaction (append-only).</summary>
    /// <param name="transaction">The decided transaction.</param>
    /// <returns>The id the store assigned (PLANNER-DECISION: store-assigned ids keep services free of randomness).</returns>
    TransactionId AppendTransaction(NewTransaction transaction);

    /// <summary>Appends an audit event (append-only).</summary>
    /// <param name="auditEvent">The event.</param>
    void AppendAudit(AuditEvent auditEvent);

    /// <summary>Sets the card's status.</summary>
    /// <param name="status">New status.</param>
    void SetStatus(CardStatus status);

    /// <summary>Creates or replaces the limit for <c>(limit.Category, limit.Period)</c>; never two rows for one pair.</summary>
    /// <param name="limit">The limit.</param>
    void UpsertLimit(SpendLimit limit);

    /// <summary>Removes the limit for a (category, period); no-op when none.</summary>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    void RemoveLimit(Category category, Period period);
}
