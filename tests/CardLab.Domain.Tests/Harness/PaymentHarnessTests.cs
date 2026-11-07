using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;
using Xunit;

namespace CardLab.Domain.Tests.Harness;

public sealed class PaymentHarnessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-U-37 · PR-02 · unit
    // Given a FakeBank, When NextDebit(Refused) then two natural debits, Hang(debitApplied: true), FailNext(error), HangStatus and HoldDebitsUntil(gate) with two concurrent debits, Then the script applies once and reverts; Hang(true) writes the entry then throws OperationCanceledException and a status check then says Completed; FailNext throws the error once; HangStatus throws once; both held debits answer only after the gate completes; DebitRequests and StatusChecks count every call
    // Catches: a fake that drifts from the scripts the application tests rely on (AC-10, AC-13, AC-16)
    [Fact]
    public async Task FakeBank_scripts_are_one_shot()
    {
        var bank = new FakeBank(new FakeClock(new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero)));

        // NextDebit: a scripted refusal moves nothing; the next debit is natural again.
        bank.NextDebit(BankOutcomes.InsufficientFunds());
        var refused = await bank.RequestDebitAsync(Debit(bank, "PAY-00000001", "10.00"), Ct);
        var natural = await bank.RequestDebitAsync(Debit(bank, "PAY-00000002", "10.00"), Ct);
        Assert.Equal(FailureReason.InsufficientFunds, Assert.IsType<BankOutcome.Refused>(refused).Reason);
        Assert.IsType<BankOutcome.Completed>(natural);
        Assert.Equal(["PAY-00000002"], bank.Entries.Select(e => e.Reference.Value));
        Assert.Equal("24990.00", bank.AvailableBalance.ToString());

        // NextDebit: a scripted completion applies the debit and returns the outcome as given.
        bank.NextDebit(BankOutcomes.Completed(null, simulated: true));
        var scripted = await bank.RequestDebitAsync(Debit(bank, "PAY-00000003", "5.00"), Ct);
        Assert.Equal(new BankOutcome.Completed(null, true), scripted);
        Assert.Equal("24985.00", bank.AvailableBalance.ToString());
        Assert.Equal(2, bank.Entries.Count);

        // Hang(debitApplied: true): the debit is written, the call then reports "no answer", a status check finds it.
        bank.Hang(debitApplied: true);
        await Assert.ThrowsAsync<OperationCanceledException>(() => bank.RequestDebitAsync(Debit(bank, "PAY-00000004", "1.00"), Ct));
        Assert.Equal("24984.00", bank.AvailableBalance.ToString());
        Assert.Equal(3, bank.Entries.Count);
        var applied = await bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000004"), Ct);
        Assert.IsType<BankOutcome.Completed>(applied);

        // Hang() without applying: nothing is written, the status check says the bank never received it.
        bank.Hang();
        await Assert.ThrowsAsync<OperationCanceledException>(() => bank.RequestDebitAsync(Debit(bank, "PAY-00000005", "1.00"), Ct));
        Assert.Equal("24984.00", bank.AvailableBalance.ToString());
        Assert.IsType<BankOutcome.NotReceived>(await bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000005"), Ct));
        var afterHangs = await bank.RequestDebitAsync(Debit(bank, "PAY-00000006", "1.00"), Ct);
        Assert.IsType<BankOutcome.Completed>(afterHangs);

        // FailNext: throws the given error once.
        var error = new InvalidOperationException("adapter failed");
        bank.FailNext(error);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => bank.RequestDebitAsync(Debit(bank, "PAY-00000007", "1.00"), Ct));
        Assert.Same(error, thrown);
        Assert.IsType<BankOutcome.Completed>(await bank.RequestDebitAsync(Debit(bank, "PAY-00000007", "1.00"), Ct));

        // NextStatus and HangStatus: each answers once, then the natural answer returns.
        var checksBefore = bank.StatusChecks;
        bank.NextStatus(BankOutcomes.AccountUnavailable(simulated: true));
        var scriptedStatus = await bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000002"), Ct);
        Assert.Equal(new BankOutcome.Refused(FailureReason.AccountUnavailable, true), scriptedStatus);
        Assert.IsType<BankOutcome.Completed>(await bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000002"), Ct));
        bank.HangStatus();
        await Assert.ThrowsAsync<OperationCanceledException>(() => bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000002"), Ct));
        Assert.IsType<BankOutcome.Completed>(await bank.GetDebitStatusAsync(bank.Account, new PaymentReference("PAY-00000002"), Ct));
        Assert.Equal(checksBefore + 4, bank.StatusChecks);

        // HoldDebitsUntil: both concurrent debits answer only after the gate completes.
        var release = new TaskCompletionSource();
        bank.HoldDebitsUntil(release.Task);
        var first = bank.RequestDebitAsync(Debit(bank, "PAY-00000008", "1.00"), Ct);
        var second = bank.RequestDebitAsync(Debit(bank, "PAY-00000009", "1.00"), Ct);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(["PAY-00000008", "PAY-00000009"], bank.DebitRequests.TakeLast(2).Select(r => r.Reference.Value));
        release.SetResult();
        Assert.IsType<BankOutcome.Completed>(await first);
        Assert.IsType<BankOutcome.Completed>(await second);

        // Every call was counted, scripted or not (PAY-00000007 twice).
        Assert.Equal(10, bank.DebitRequests.Count);
    }

    private static DebitRequest Debit(FakeBank bank, string reference, string amount) =>
        new(bank.Account, new PaymentReference(reference), TestMoney.Of(amount), $"CardLab card payment {reference}");

    // T-U-37 · PR-02 · unit
    // Given FakePaymentReferences, When Next three times (also from parallel callers), Then PAY-00000001, PAY-00000002, PAY-00000003 (distinct, each matching PaymentReference.Pattern) and Issued lists them
    // Catches: non-deterministic or invalid references in unit tests (DEC-8)
    [Fact]
    public void FakePaymentReferences_are_sequential_and_valid()
    {
        var references = new FakePaymentReferences();

        var first = references.Next();
        var second = references.Next();
        var third = references.Next();
        var parallel = new FakePaymentReferences();
        var fromParallelCallers = new PaymentReference[50];
        Parallel.For(0, fromParallelCallers.Length, i => fromParallelCallers[i] = parallel.Next());

        Assert.Equal(["PAY-00000001", "PAY-00000002", "PAY-00000003"], new[] { first.Value, second.Value, third.Value });
        Assert.Equal([first, second, third], references.Issued);
        Assert.All(references.Issued, r => Assert.Matches(PaymentReference.Pattern, r.Value));
        Assert.Equal(50, fromParallelCallers.Distinct().Count());
        Assert.Equal(50, parallel.Issued.Count);
    }

    // T-U-37 · PR-02 · unit
    // Given text with "51907731", a 16-digit card number, the BankAccountRef GUID and "25000.00", When FindAccountNumbers / AssertNoFullNumbers, Then only the account number is found by FindAccountNumbers, and AssertNoFullNumbers throws for the account number or the card number (message shows last fours only) but not for the GUID or amounts
    // Catches: a scanner too weak to catch the account number, or one that flags GUIDs (NFR-3, AC-23)
    [Fact]
    public void PanScanner_finds_the_seeded_account_number()
    {
        var guid = Seed.BankAccountRef.ToString();
        var text = $"account 51907731 card 9999000100000001 ref {guid} balance 25000.00";

        var accountNumbers = PanScanner.FindAccountNumbers(text);
        var onAccount = Assert.ThrowsAny<Exception>(() => PanScanner.AssertNoFullNumbers("debit from 51907731 failed", "log"));
        var onCard = Assert.ThrowsAny<Exception>(() => PanScanner.AssertNoFullNumbers("card 9999000100000001", "log"));

        Assert.Equal(["51907731"], accountNumbers);
        Assert.Contains("...7731", onAccount.Message);
        Assert.DoesNotContain("51907731", onAccount.Message);
        Assert.Contains("...0001", onCard.Message);
        Assert.DoesNotContain("9999000100000001", onCard.Message);
        PanScanner.AssertNoFullNumbers($"ref {guid} balance 25000.00 ending 7731", "log");
        Assert.Empty(PanScanner.FindAccountNumbers(null));
    }
}
