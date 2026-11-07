using CardLab.Domain;
using CardLab.Domain.Ports;
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
    [Fact(Skip = "todo")]
    public Task Reads_the_account() => Task.CompletedTask;

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given £25,000.00, When RequestDebitAsync(PAY-00000001, £150.50), Then Completed (not simulated), available £24,849.50 and ListRecentEntriesAsync lists one entry with reference PAY-00000001 and £150.50
    // Catches: a completed debit not reflected on the account side (AC-7, R-9, R-13)
    /// <summary>Contract B-2.</summary>
    [Fact(Skip = "todo")]
    public Task Natural_debit_completes_and_posts_an_entry() => Task.CompletedTask;

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given a completed debit for PAY-00000001, When the same request is sent again, Then the same outcome, still one entry and the balance reduced once
    // Catches: a retried debit applied twice (R-7, port contract)
    /// <summary>Contract B-3.</summary>
    [Fact(Skip = "todo")]
    public Task Repeated_reference_is_never_applied_twice() => Task.CompletedTask;

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given £25,000.00, When a debit of £25,000.01 is requested, Then Refused(InsufficientFunds), no entry, balance unchanged; a later status check by that reference says Refused(InsufficientFunds)
    // Catches: an overdrawing debit accepted (AC-31, R-15)
    /// <summary>Contract B-4.</summary>
    [Fact(Skip = "todo")]
    public Task Debit_above_the_available_balance_is_refused() => Task.CompletedTask;

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given a completed debit and an unknown reference, When GetDebitStatusAsync, Then Completed (with the debit time) for the first and NotReceived for the second
    // Catches: status checks unable to resolve a pending payment (R-11)
    /// <summary>Contract B-5.</summary>
    [Fact(Skip = "todo")]
    public Task Status_by_reference_answers_completed_or_not_received() => Task.CompletedTask;

    // T-U-24 / T-I-15 · PR-02 · contract
    // Given 12 completed debits at increasing times, When ListRecentEntriesAsync(take 10), Then 10 entries newest first
    // Catches: oldest-first or unbounded account activity (R-13)
    /// <summary>Contract B-6.</summary>
    [Fact(Skip = "todo")]
    public Task Recent_entries_are_newest_first_and_bounded() => Task.CompletedTask;
}
