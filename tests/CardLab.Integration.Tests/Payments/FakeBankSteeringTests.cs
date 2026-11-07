using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.FakeBank;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class FakeBankSteeringTests : IClassFixture<SqlTestDatabase>
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
    private readonly SqlTestDatabase _fixture;

    public FakeBankSteeringTests(SqlTestDatabase fixture) => _fixture = fixture;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-20 · PR-05 · integration
    // Given SqlFakeBank with steering RefuseAccountUnavailable, When two debits are requested, Then the first is Refused(AccountUnavailable, simulated) with nothing written and the second completes naturally (not simulated); steering Complete marks the completion simulated
    // Catches: steering applying more than once or steered outcomes not marked (AC-32, AC-33, R-15)
    [Fact]
    public async Task Steering_is_one_shot_and_marks_simulated()
    {
        var (bank, account) = await NewBankAsync(100m);
        await bank.SetAsync(new Steering(NextDebitOutcome.RefuseAccountUnavailable, null), Ct);

        var first = await bank.RequestDebitAsync(Debit(account, "PAY-STEER001", "10.00"), Ct);
        var afterFirst = await bank.ListRecentEntriesAsync(account, 10, Ct);
        var balanceAfterFirst = (await bank.GetAccountAsync(account, Ct)).AvailableBalance.ToString();
        var second = await bank.RequestDebitAsync(Debit(account, "PAY-STEER002", "10.00"), Ct);
        await bank.SetAsync(new Steering(NextDebitOutcome.Complete, null), Ct);
        var third = await bank.RequestDebitAsync(Debit(account, "PAY-STEER003", "10.00"), Ct);

        Assert.Equal(new BankOutcome.Refused(FailureReason.AccountUnavailable, true), first);
        Assert.Empty(afterFirst);
        Assert.Equal("100.00", balanceAfterFirst);
        Assert.False(Assert.IsType<BankOutcome.Completed>(second).Simulated);
        Assert.True(Assert.IsType<BankOutcome.Completed>(third).Simulated);
        Assert.Equal("80.00", (await bank.GetAccountAsync(account, Ct)).AvailableBalance.ToString());
        Assert.Equal(new Steering(null, null), await bank.GetAsync(Ct));
    }

    // T-I-20 · PR-05 · integration
    // Given steering TimeoutDebitApplied, When a debit is requested with a 1 s token, Then OperationCanceledException after ~1 s, yet the entry and the reduced balance are committed and a later status check says Completed with the debit time; with TimeoutNotReceived nothing is written and the status check says NotReceived
    // Catches: timeout modes writing the wrong side or returning instead of hanging (AC-13, AC-14, AC-15, DEC-6)
    [Fact]
    public async Task Timeout_modes_hang_after_writing_or_not()
    {
        var (bank, account) = await NewBankAsync(100m);
        await bank.SetAsync(new Steering(NextDebitOutcome.TimeoutDebitApplied, null), Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bank.RequestDebitAsync(Debit(account, "PAY-STEER011", "10.00"), Budget()));
        var applied = await bank.GetDebitStatusAsync(account, new PaymentReference("PAY-STEER011"), Ct);

        Assert.Equal(new BankOutcome.Completed(T0, true), applied);
        Assert.Single(await bank.ListRecentEntriesAsync(account, 10, Ct));
        Assert.Equal("90.00", (await bank.GetAccountAsync(account, Ct)).AvailableBalance.ToString());

        await bank.SetAsync(new Steering(NextDebitOutcome.TimeoutNotReceived, null), Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bank.RequestDebitAsync(Debit(account, "PAY-STEER012", "10.00"), Budget()));
        var notReceived = await bank.GetDebitStatusAsync(account, new PaymentReference("PAY-STEER012"), Ct);

        Assert.IsType<BankOutcome.NotReceived>(notReceived);
        Assert.Single(await bank.ListRecentEntriesAsync(account, 10, Ct));
        Assert.Equal("90.00", (await bank.GetAccountAsync(account, Ct)).AvailableBalance.ToString());
        Assert.Equal(new Steering(null, null), await bank.GetAsync(Ct));
    }

    // T-I-20 · PR-05 · integration
    // Given NextStatusCheck DoesNotAnswer, When two status checks run (1 s token), Then the first throws OperationCanceledException (simulated) and the second answers; steering never changes balance or entries
    // Catches: unanswered status checks unreachable for AC-16 (DEC-7)
    [Fact]
    public async Task Status_check_steering_is_one_shot()
    {
        var (bank, account) = await NewBankAsync(100m);
        await bank.RequestDebitAsync(Debit(account, "PAY-STEER021", "10.00"), Ct);
        await bank.SetAsync(new Steering(null, NextStatusCheck.DoesNotAnswer), Ct);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bank.GetDebitStatusAsync(account, new PaymentReference("PAY-STEER021"), Budget()));
        var second = await bank.GetDebitStatusAsync(account, new PaymentReference("PAY-STEER021"), Ct);

        Assert.Equal(new BankOutcome.Completed(T0, false), second);
        Assert.Single(await bank.ListRecentEntriesAsync(account, 10, Ct));
        Assert.Equal("90.00", (await bank.GetAccountAsync(account, Ct)).AvailableBalance.ToString());
        Assert.Equal(new Steering(null, null), await bank.GetAsync(Ct));
    }

    // A fresh account (and bank) per test, with the shared steering row cleared, so tests cannot see each other's state.
    private async Task<(SqlFakeBank Bank, BankAccountRef Account)> NewBankAsync(decimal balance)
    {
        var account = new BankAccountRef(Guid.NewGuid());
        await using (var setup = _fixture.CreateDbContext())
        {
            setup.BankAccounts.Add(new BankAccountRow { Id = account.Value, LastFour = "7731", AvailableBalance = balance });
            await setup.SaveChangesAsync(Ct);
        }

        var bank = new SqlFakeBank(_fixture.CreateDbContext(), new FakeClock(T0));
        await bank.SetAsync(new Steering(null, null), Ct);
        return (bank, account);
    }

    // The caller's time budget: it fires, which is what "the bank never answers" means to the caller.
    private static CancellationToken Budget()
    {
        var budget = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        budget.CancelAfter(TimeSpan.FromSeconds(1));
        return budget.Token;
    }

    private static DebitRequest Debit(BankAccountRef account, string reference, string amount) =>
        new(account, new PaymentReference(reference), TestMoney.Of(amount), $"CardLab card payment {reference}");
}
