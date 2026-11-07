using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentStateMachineTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 10, 5, 0, TimeSpan.Zero);

    // T-U-7 · PR-01 · unit
    // Given Pending, When Completed(at) / Refused(InsufficientFunds) / Refused(AccountUnavailable) / NotReceived / Unknown, Then Completed with debit time at / Failed(InsufficientFunds) / Failed(AccountUnavailable) / Failed(NotReceived) / Pending → Pending; Given Completed or Failed, When any outcome, Then Fail
    // Catches: a terminal payment changing state, or Unknown failing the payment (AC-17, R-6, R-11)
    [Fact]
    public void Pending_completes_with_the_banks_debit_time()
    {
        var result = PaymentStateMachine.Apply(PaymentState.Pending, BankOutcomes.Completed(At));

        Assert.True(result.IsOk);
        Assert.Equal(new Transition(PaymentState.Pending, PaymentState.Completed, null, At), result.Value);
    }

    [Fact]
    public void Pending_completes_without_a_debit_time_when_the_bank_gave_none()
    {
        var result = PaymentStateMachine.Apply(PaymentState.Pending, BankOutcomes.Completed());

        Assert.True(result.IsOk);
        Assert.Equal(new Transition(PaymentState.Pending, PaymentState.Completed, null, null), result.Value);
    }

    [Theory]
    [InlineData(FailureReason.InsufficientFunds)]
    [InlineData(FailureReason.AccountUnavailable)]
    public void Pending_fails_with_the_banks_refusal_reason(FailureReason reason)
    {
        var result = PaymentStateMachine.Apply(PaymentState.Pending, new BankOutcome.Refused(reason, false));

        Assert.True(result.IsOk);
        Assert.Equal(new Transition(PaymentState.Pending, PaymentState.Failed, reason, null), result.Value);
    }

    [Fact]
    public void Pending_fails_as_not_received()
    {
        var result = PaymentStateMachine.Apply(PaymentState.Pending, BankOutcomes.NotReceived());

        Assert.True(result.IsOk);
        Assert.Equal(new Transition(PaymentState.Pending, PaymentState.Failed, FailureReason.NotReceived, null), result.Value);
    }

    [Fact]
    public void Pending_stays_pending_when_the_bank_does_not_answer()
    {
        var result = PaymentStateMachine.Apply(PaymentState.Pending, BankOutcomes.Unknown());

        Assert.True(result.IsOk);
        Assert.Equal(new Transition(PaymentState.Pending, PaymentState.Pending, null, null), result.Value);
    }

    [Theory]
    [InlineData(PaymentState.Completed, "payment already completed")]
    [InlineData(PaymentState.Failed, "payment already failed")]
    public void Terminal_states_reject_every_outcome(PaymentState terminal, string message)
    {
        BankOutcome[] outcomes =
        [
            BankOutcomes.Completed(At),
            BankOutcomes.InsufficientFunds(),
            BankOutcomes.AccountUnavailable(),
            BankOutcomes.NotReceived(),
            BankOutcomes.Unknown(),
        ];

        foreach (var outcome in outcomes)
        {
            var result = PaymentStateMachine.Apply(terminal, outcome);

            Assert.False(result.IsOk);
            Assert.Equal(message, result.Error);
        }
    }
}
