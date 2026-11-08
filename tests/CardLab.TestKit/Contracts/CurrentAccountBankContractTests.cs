using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;
using Xunit;

namespace CardLab.TestKit.Contracts;

/// <summary>
/// 002: the port contract of <see cref="ICurrentAccountBank"/> (design §5, §15.2), run against <c>FakeBank</c> (T-U-24,
/// <c>tests/CardLab.Domain.Tests/Fakes/FakeBankContractTests.cs</c>) and <c>SqlFakeBank</c> (T-I-15,
/// <c>tests/CardLab.Integration.Tests/Payments/FakeBankContractTests.cs</c>). Natural behaviour only (no steering, no hangs).
/// Bodies written in PR-02; PR-05 makes them pass against SQL without editing this file.
/// </summary>
public abstract class CurrentAccountBankContractTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
    private static int nextBlock;

    // xUnit creates one instance per test: each test gets its own block of references and its own account key, so cases
    // never collide on keys in a database shared by the whole class (counters, never random).
    private readonly int block = Interlocked.Increment(ref nextBlock);

    /// <summary>
    /// The clock the bank under test must post and debit by (fresh per test, starts at 2026-11-07T10:00Z). Implementations
    /// of <see cref="CreateBankAsync"/> pass it to the bank; tests advance it to separate debit times.
    /// </summary>
    protected FakeClock Clock { get; } = new(T0);
    /// <summary>Creates a bank holding one account with no entries.</summary>
    /// <param name="account">Account key (fresh per test).</param>
    /// <param name="lastFour">Last four digits.</param>
    /// <param name="openingBalance">Available balance.</param>
    /// <returns>The bank under test.</returns>
    protected abstract Task<ICurrentAccountBank> CreateBankAsync(BankAccountRef account, string lastFour, Money openingBalance);

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given a bank created with account ref R, last four 7731 and £25,000.00, When GetAccountAsync(R), Then last four 7731 and available £25,000.00
    // Catches: wrong account or balance shown (R-2, AC-4)
    /// <summary>Contract B-1.</summary>
    [Fact]
    public async Task Reads_the_account()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));

        var snapshot = await bank.GetAccountAsync(account);

        Assert.Equal(account, snapshot.Ref);
        Assert.Equal("7731", snapshot.LastFour);
        Assert.Equal("25000.00", snapshot.AvailableBalance.ToString());
    }

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given £25,000.00, When RequestDebitAsync(PAY-00000001, £150.50), Then Completed (not simulated), available £24,849.50 and ListRecentEntriesAsync lists one entry with reference PAY-00000001 and £150.50
    // Catches: a completed debit not reflected on the account side (AC-7, R-9, R-13)
    /// <summary>Contract B-2.</summary>
    [Fact]
    public async Task Natural_debit_completes_and_posts_an_entry()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));

        var outcome = await bank.RequestDebitAsync(Debit(account, Ref(1).Value, "150.50"));

        var completed = Assert.IsType<BankOutcome.Completed>(outcome);
        Assert.False(completed.Simulated);
        Assert.Equal("24849.50", (await bank.GetAccountAsync(account)).AvailableBalance.ToString());
        var entry = Assert.Single(await bank.ListRecentEntriesAsync(account, 10));
        Assert.Equal((Ref(1).Value, "150.50"), (entry.Reference.Value, entry.Amount.ToString()));
    }

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given a completed debit for PAY-00000001, When the same request is sent again, Then the same outcome, still one entry and the balance reduced once
    // Catches: a retried debit applied twice (R-7, port contract)
    /// <summary>Contract B-3.</summary>
    [Fact]
    public async Task Repeated_reference_is_never_applied_twice()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));
        var request = Debit(account, Ref(1).Value, "150.50");
        var first = await bank.RequestDebitAsync(request);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var second = await bank.RequestDebitAsync(request);

        var firstCompleted = Assert.IsType<BankOutcome.Completed>(first);
        var secondCompleted = Assert.IsType<BankOutcome.Completed>(second);
        Assert.Equal((DateTimeOffset?)T0, Millis(firstCompleted.DebitedAtUtc));
        Assert.Equal((DateTimeOffset?)T0, Millis(secondCompleted.DebitedAtUtc));
        Assert.Single(await bank.ListRecentEntriesAsync(account, 10));
        Assert.Equal("24849.50", (await bank.GetAccountAsync(account)).AvailableBalance.ToString());
    }

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given £25,000.00, When a debit of £25,000.01 is requested, Then Refused(InsufficientFunds), no entry, balance unchanged; a later status check by that reference says Refused(InsufficientFunds)
    // Catches: an overdrawing debit accepted (AC-31, R-15)
    /// <summary>Contract B-4.</summary>
    [Fact]
    public async Task Debit_above_the_available_balance_is_refused()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));

        var outcome = await bank.RequestDebitAsync(Debit(account, Ref(1).Value, "25000.01"));
        var status = await bank.GetDebitStatusAsync(account, Ref(1));

        var refused = Assert.IsType<BankOutcome.Refused>(outcome);
        Assert.Equal(FailureReason.InsufficientFunds, refused.Reason);
        Assert.Empty(await bank.ListRecentEntriesAsync(account, 10));
        Assert.Equal("25000.00", (await bank.GetAccountAsync(account)).AvailableBalance.ToString());
        Assert.Equal(FailureReason.InsufficientFunds, Assert.IsType<BankOutcome.Refused>(status).Reason);
    }

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given a completed debit and an unknown reference, When GetDebitStatusAsync, Then Completed (with the debit time) for the first and NotReceived for the second
    // Catches: status checks unable to resolve a pending payment (R-11)
    /// <summary>Contract B-5.</summary>
    [Fact]
    public async Task Status_by_reference_answers_completed_or_not_received()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));
        await bank.RequestDebitAsync(Debit(account, Ref(1).Value, "150.50"));

        var known = await bank.GetDebitStatusAsync(account, Ref(1));
        var unknown = await bank.GetDebitStatusAsync(account, Ref(2));

        var completed = Assert.IsType<BankOutcome.Completed>(known);
        Assert.Equal((DateTimeOffset?)T0, Millis(completed.DebitedAtUtc));
        Assert.IsType<BankOutcome.NotReceived>(unknown);
    }

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given 12 completed debits at increasing times, When ListRecentEntriesAsync(take 10), Then 10 entries newest first
    // Catches: oldest-first or unbounded account activity (R-13)
    /// <summary>Contract B-6.</summary>
    [Fact]
    public async Task Recent_entries_are_newest_first_and_bounded()
    {
        var account = NewAccount();
        var bank = await CreateBankAsync(account, "7731", TestMoney.Of("25000.00"));
        for (var i = 1; i <= 12; i++)
        {
            await bank.RequestDebitAsync(Debit(account, Ref(i).Value, "1.00"));
            Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var entries = await bank.ListRecentEntriesAsync(account, 10);

        Assert.Equal(
            [Ref(12).Value, Ref(11).Value, Ref(10).Value, Ref(9).Value, Ref(8).Value, Ref(7).Value, Ref(6).Value, Ref(5).Value, Ref(4).Value, Ref(3).Value],
            entries.Select(e => e.Reference.Value));
        Assert.Equal(T0.AddSeconds(11), entries[0].PostedAtUtc);
    }

    private PaymentReference Ref(int n) => new($"PAY-{block:D5}{n:D3}");

    private BankAccountRef NewAccount() => new(new Guid(block, 0xBA, 0xBA, 0, 0, 0, 0, 0, 0, 0, 0));

    private static DebitRequest Debit(BankAccountRef account, string reference, string amount) =>
        new(account, new PaymentReference(reference), TestMoney.Of(amount), $"CardLab card payment {reference}");

    private static DateTimeOffset? Millis(DateTimeOffset? at) =>
        at is null ? null : new DateTimeOffset(at.Value.UtcTicks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
}
