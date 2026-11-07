using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;

namespace CardLab.Infrastructure.FakeBank;

/// <summary>
/// The persisted fake current account (R-15, DEC-6, ADR-007): tables <c>BankAccount</c>, <c>BankDebitRequest</c>,
/// <c>BankEntry</c>, <c>BankSteering</c> in the CardLab database, never joined with card tables (DEC-2). Shipped to every
/// environment. Must pass <c>CurrentAccountBankContractTests</c> (T-I-15).
/// </summary>
/// <remarks>
/// Debit (design §8 flow 6): own transaction with <c>sp_getapplock</c> on resource <c>bank:{accountId}</c> (5 s); a known
/// reference returns the recorded outcome (idempotent); else read-and-clear <c>BankSteering.NextDebitOutcome</c>
/// (Simulated = a value was set); refuse InsufficientFunds when steered or when Amount &gt; AvailableBalance; Complete and
/// TimeoutDebitApplied insert <c>BankDebitRequest</c> + <c>BankEntry</c> and decrement the balance, commit;
/// TimeoutNotReceived commits nothing; both timeout modes then <c>await Task.Delay(Timeout.Infinite, ct)</c>. Status:
/// read-and-clear <c>NextStatusCheck</c>; DoesNotAnswer waits on <c>ct</c>; else Completed(DebitedAtUtc) / Refused(reason)
/// from the request row, NotReceived when none. Shares the request's scoped <see cref="CardLabDbContext"/>.
/// Implemented in PR-05.
/// </remarks>
public sealed class SqlFakeBank : ICurrentAccountBank, IFakeBankSteering
{
    private readonly CardLabDbContext _db;
    private readonly IClock _clock;

    /// <summary>Creates the adapter.</summary>
    /// <param name="db">Scoped context.</param>
    /// <param name="clock">Clock for receive, debit and posting times.</param>
    public SqlFakeBank(CardLabDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<AccountSnapshot> GetAccountAsync(BankAccountRef account, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<IReadOnlyList<BankEntry>> ListRecentEntriesAsync(BankAccountRef account, int take, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<BankOutcome> RequestDebitAsync(DebitRequest request, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<BankOutcome> GetDebitStatusAsync(BankAccountRef account, PaymentReference reference, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<Steering> GetAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task SetAsync(Steering steering, CancellationToken ct = default) => throw new NotImplementedException("PR-05");
}
