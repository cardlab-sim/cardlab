using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Builders;

namespace CardLab.TestKit.Fakes;

/// <summary>
/// Scriptable in-memory <see cref="ICurrentAccountBank"/> for unit tests (design §15.2). Natural behaviour matches the
/// port contract (idempotent on reference; refuses above the available balance; status by reference) and it must pass
/// <c>CurrentAccountBankContractTests</c> (T-U-24). Scripts are one-shot (except <see cref="HoldDebitsUntil"/>) and override the natural behaviour once; a reference the bank has already seen returns its recorded outcome and leaves the scripts unconsumed (design §8 Flow 6).
/// </summary>
/// <remarks>
/// PLANNER-DECISION: <see cref="Hang"/> and <see cref="HangStatus"/> wait on the caller's token and then throw
/// <see cref="SimulatedNoAnswerException"/>, as the port contract says (a steered no-answer). Unit tests that hang build the
/// service with a zero budget (<c>bankTimeoutSeconds: 0</c>) so the wait ends at once. Ledger times come from the clock passed in (never the system clock).
/// Thread-safe.
/// </remarks>
public sealed class FakeBank : ICurrentAccountBank
{
    private readonly object gate = new();
    private readonly IClock clock;
    private readonly string lastFour;
    private readonly List<BankEntry> entries = [];
    private readonly List<DebitRequest> debitRequests = [];
    private readonly Dictionary<PaymentReference, BankOutcome> recorded = [];
    private Money balance;
    private int statusChecks;
    private BankOutcome? nextDebit;
    private bool? hang;
    private Exception? failNext;
    private BankOutcome? nextStatus;
    private bool hangStatus;
    private Task? holdDebits;

    /// <summary>Creates the bank with the seeded account (7731, £25,000.00).</summary>
    /// <param name="clock">Clock for posting and debit times.</param>
    public FakeBank(IClock clock)
        : this(clock, new BankAccountRef(Seed.BankAccountRef), Seed.LinkedAccountLastFour, TestMoney.Of(Seed.BankOpeningBalance))
    {
    }

    /// <summary>Creates the bank with a given account.</summary>
    /// <param name="clock">Clock for posting and debit times.</param>
    /// <param name="account">Account key.</param>
    /// <param name="lastFour">Last four digits.</param>
    /// <param name="openingBalance">Available balance.</param>
    public FakeBank(IClock clock, BankAccountRef account, string lastFour, Money openingBalance)
    {
        this.clock = clock;
        Account = account;
        this.lastFour = lastFour;
        balance = openingBalance;
    }

    /// <summary>The account key.</summary>
    public BankAccountRef Account { get; }

    /// <summary>Current available balance.</summary>
    public Money AvailableBalance
    {
        get
        {
            lock (gate)
            {
                return balance;
            }
        }
    }

    /// <summary>Ledger entries, newest first.</summary>
    public IReadOnlyList<BankEntry> Entries
    {
        get
        {
            lock (gate)
            {
                return NewestFirst();
            }
        }
    }

    /// <summary>Every <see cref="RequestDebitAsync"/> call received (repeats included), in order.</summary>
    public IReadOnlyList<DebitRequest> DebitRequests
    {
        get
        {
            lock (gate)
            {
                return debitRequests.ToArray();
            }
        }
    }

    /// <summary>Number of <see cref="GetDebitStatusAsync"/> calls.</summary>
    public int StatusChecks
    {
        get
        {
            lock (gate)
            {
                return statusChecks;
            }
        }
    }

    /// <summary>Scripts the next debit's answer: Completed (applies the debit, posts an entry) or Refused (nothing moves).</summary>
    /// <param name="outcome">The outcome to return (its <c>Simulated</c> flag is returned as given).</param>
    public void NextDebit(BankOutcome outcome)
    {
        lock (gate)
        {
            nextDebit = outcome;
        }
    }

    /// <summary>The next debit never answers: optionally applies it first, then waits for the caller's token and throws <see cref="SimulatedNoAnswerException"/>.</summary>
    /// <param name="debitApplied">True: ledger written and remembered as completed (a later status check says Completed).</param>
    public void Hang(bool debitApplied = false)
    {
        lock (gate)
        {
            hang = debitApplied;
        }
    }

    /// <summary>The next debit throws <paramref name="error"/> (adapter failure, design §9).</summary>
    /// <param name="error">The exception.</param>
    public void FailNext(Exception error)
    {
        lock (gate)
        {
            failNext = error;
        }
    }

    /// <summary>Scripts the next status check's answer (Completed / Refused / NotReceived).</summary>
    /// <param name="outcome">The outcome.</param>
    public void NextStatus(BankOutcome outcome)
    {
        lock (gate)
        {
            nextStatus = outcome;
        }
    }

    /// <summary>The next status check never answers: it waits for the caller's token, then throws <see cref="SimulatedNoAnswerException"/>.</summary>
    public void HangStatus()
    {
        lock (gate)
        {
            hangStatus = true;
        }
    }

    /// <summary>Gate for AC-10: debit calls wait for <paramref name="release"/> before answering.</summary>
    /// <param name="release">Completes when the held debits may answer.</param>
    /// <remarks>Stays in force until replaced (pass <see cref="Task.CompletedTask"/> to clear).</remarks>
    public void HoldDebitsUntil(Task release)
    {
        lock (gate)
        {
            holdDebits = release;
        }
    }

    /// <inheritdoc />
    public Task<AccountSnapshot> GetAccountAsync(BankAccountRef account, CancellationToken ct = default)
    {
        lock (gate)
        {
            CheckAccount(account);
            return Task.FromResult(new AccountSnapshot(Account, lastFour, balance));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BankEntry>> ListRecentEntriesAsync(BankAccountRef account, int take, CancellationToken ct = default)
    {
        lock (gate)
        {
            CheckAccount(account);
            return Task.FromResult<IReadOnlyList<BankEntry>>(NewestFirst().Take(take).ToArray());
        }
    }

    /// <inheritdoc />
    public async Task<BankOutcome> RequestDebitAsync(DebitRequest request, CancellationToken ct = default)
    {
        Task? held;
        lock (gate)
        {
            CheckAccount(request.Account);
            debitRequests.Add(request);
            held = holdDebits;
        }

        if (held is not null)
        {
            await held.WaitAsync(ct);
        }

        bool hangs;
        BankOutcome? answer;
        lock (gate)
        {
            hangs = Debit(request, out answer);
        }

        if (hangs)
        {
            await NeverAnswerAsync(ct);
        }

        return answer!;
    }

    /// <inheritdoc />
    public async Task<BankOutcome> GetDebitStatusAsync(BankAccountRef account, PaymentReference reference, CancellationToken ct = default)
    {
        lock (gate)
        {
            CheckAccount(account);
            statusChecks++;
            if (!hangStatus)
            {
                if (nextStatus is { } scripted)
                {
                    nextStatus = null;
                    return scripted;
                }

                return recorded.TryGetValue(reference, out var seen) ? seen : BankOutcomes.NotReceived();
            }

            hangStatus = false;
        }

        await NeverAnswerAsync(ct);
        throw new InvalidOperationException("Unreachable: the wait only ends by cancellation.");
    }

    private static async Task NeverAnswerAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            throw new SimulatedNoAnswerException(ct);
        }
    }

    // Returns true when the debit hangs (no answer); otherwise the answer is in "answer".
    private bool Debit(DebitRequest request, out BankOutcome? answer)
    {
        answer = null;
        if (failNext is { } error)
        {
            failNext = null;
            throw error;
        }

        if (recorded.TryGetValue(request.Reference, out var seen))
        {
            answer = seen;
            return false;
        }

        if (hang is { } applied)
        {
            hang = null;
            if (applied)
            {
                var at = clock.UtcNow;
                Apply(request, at);
                recorded[request.Reference] = BankOutcomes.Completed(at);
            }

            return true;
        }

        if (nextDebit is { } scripted)
        {
            nextDebit = null;
            if (scripted is BankOutcome.Completed)
            {
                Apply(request, clock.UtcNow);
            }

            recorded[request.Reference] = scripted;
            answer = scripted;
            return false;
        }

        BankOutcome outcome;
        if (request.Amount > balance)
        {
            outcome = BankOutcomes.InsufficientFunds();
        }
        else
        {
            var at = clock.UtcNow;
            Apply(request, at);
            outcome = BankOutcomes.Completed(at);
        }

        recorded[request.Reference] = outcome;
        answer = outcome;
        return false;
    }

    private void Apply(DebitRequest request, DateTimeOffset at)
    {
        balance -= request.Amount;
        entries.Add(new BankEntry(request.Reference, request.Amount, at, request.Narrative));
    }

    private BankEntry[] NewestFirst() => Enumerable.Reverse(entries).OrderByDescending(e => e.PostedAtUtc).ToArray();

    private void CheckAccount(BankAccountRef account)
    {
        if (account != Account)
        {
            throw new KeyNotFoundException("The fake bank holds a different account.");
        }
    }
}
