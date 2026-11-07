using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;

namespace CardLab.TestKit.Fakes;

/// <summary>
/// Scriptable in-memory <see cref="ICurrentAccountBank"/> for unit tests (design §15.2). Natural behaviour matches the
/// port contract (idempotent on reference; refuses above the available balance; status by reference) and it must pass
/// <c>CurrentAccountBankContractTests</c> (T-U-24). Scripts are one-shot and override the natural behaviour once.
/// </summary>
/// <remarks>
/// PLANNER-DECISION: <see cref="Hang"/> throws <see cref="OperationCanceledException"/> immediately (it stands for "the
/// budget fired"), so unit tests never wait. Ledger times come from the clock passed in (never the system clock).
/// Thread-safe. Implemented in PR-02.
/// </remarks>
public sealed class FakeBank : ICurrentAccountBank
{
    /// <summary>Creates the bank with the seeded account (7731, £25,000.00).</summary>
    /// <param name="clock">Clock for posting and debit times.</param>
    public FakeBank(IClock clock) => throw new NotImplementedException("PR-02");

    /// <summary>Creates the bank with a given account.</summary>
    /// <param name="clock">Clock for posting and debit times.</param>
    /// <param name="account">Account key.</param>
    /// <param name="lastFour">Last four digits.</param>
    /// <param name="openingBalance">Available balance.</param>
    public FakeBank(IClock clock, BankAccountRef account, string lastFour, Money openingBalance) => throw new NotImplementedException("PR-02");

    /// <summary>The account key.</summary>
    public BankAccountRef Account => throw new NotImplementedException("PR-02");

    /// <summary>Current available balance.</summary>
    public Money AvailableBalance => throw new NotImplementedException("PR-02");

    /// <summary>Ledger entries, newest first.</summary>
    public IReadOnlyList<BankEntry> Entries => throw new NotImplementedException("PR-02");

    /// <summary>Every <see cref="RequestDebitAsync"/> call received (repeats included), in order.</summary>
    public IReadOnlyList<DebitRequest> DebitRequests => throw new NotImplementedException("PR-02");

    /// <summary>Number of <see cref="GetDebitStatusAsync"/> calls.</summary>
    public int StatusChecks => throw new NotImplementedException("PR-02");

    /// <summary>Scripts the next debit's answer: Completed (applies the debit, posts an entry) or Refused (nothing moves).</summary>
    /// <param name="outcome">The outcome to return (its <c>Simulated</c> flag is returned as given).</param>
    public void NextDebit(BankOutcome outcome) => throw new NotImplementedException("PR-02");

    /// <summary>The next debit never answers: optionally applies it first, then throws <see cref="OperationCanceledException"/>.</summary>
    /// <param name="debitApplied">True: ledger written and remembered as completed (a later status check says Completed).</param>
    public void Hang(bool debitApplied = false) => throw new NotImplementedException("PR-02");

    /// <summary>The next debit throws <paramref name="error"/> (adapter failure, design §9).</summary>
    /// <param name="error">The exception.</param>
    public void FailNext(Exception error) => throw new NotImplementedException("PR-02");

    /// <summary>Scripts the next status check's answer (Completed / Refused / NotReceived).</summary>
    /// <param name="outcome">The outcome.</param>
    public void NextStatus(BankOutcome outcome) => throw new NotImplementedException("PR-02");

    /// <summary>The next status check never answers (throws <see cref="OperationCanceledException"/>).</summary>
    public void HangStatus() => throw new NotImplementedException("PR-02");

    /// <summary>Gate for AC-10: debit calls wait for <paramref name="release"/> before answering.</summary>
    /// <param name="release">Completes when the held debits may answer.</param>
    public void HoldDebitsUntil(Task release) => throw new NotImplementedException("PR-02");

    /// <inheritdoc />
    public Task<AccountSnapshot> GetAccountAsync(BankAccountRef account, CancellationToken ct = default) => throw new NotImplementedException("PR-02");

    /// <inheritdoc />
    public Task<IReadOnlyList<BankEntry>> ListRecentEntriesAsync(BankAccountRef account, int take, CancellationToken ct = default) => throw new NotImplementedException("PR-02");

    /// <inheritdoc />
    public Task<BankOutcome> RequestDebitAsync(DebitRequest request, CancellationToken ct = default) => throw new NotImplementedException("PR-02");

    /// <inheritdoc />
    public Task<BankOutcome> GetDebitStatusAsync(BankAccountRef account, PaymentReference reference, CancellationToken ct = default) => throw new NotImplementedException("PR-02");
}
