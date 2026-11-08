using CardLab.Domain.Application;
using CardLab.Domain.Payments;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Application;

public sealed class InFlightPaymentsTests
{
    private static readonly PaymentReference First = new("PAY-00000001");
    private static readonly PaymentReference Second = new("PAY-00000002");

    private static Receipt ReceiptFor(PaymentReference reference) =>
        new(
            reference,
            PaymentState.Completed,
            TestMoney.Of("150.50"),
            Money.Zero,
            QuoteBuilder.DefaultAt,
            "from",
            "to",
            null,
            "message",
            Money.Zero,
            false,
            PaymentOrigin.CardholderView);

    // T-U-30 · PR-04 · unit (application)
    // Given a registered task for one reference, When TryGet, Then it is that task while the owner is running and gone after Complete or Fail; another reference is independent and may be registered again afterwards
    // Catches: the registry leaking entries or mixing references (AC-10, DEC-5)
    [Fact]
    public async Task Registered_task_is_found_until_the_owner_completes_or_fails()
    {
        var registry = new InFlightPayments();
        var owner = new TaskCompletionSource<Receipt>();

        registry.Register(First, owner.Task);

        Assert.True(registry.TryGet(First, out var found));
        Assert.Same(owner.Task, found);
        Assert.False(registry.TryGet(Second, out _));

        registry.Complete(First, owner, ReceiptFor(First));
        Assert.Equal(First, (await owner.Task).Reference);
        Assert.False(registry.TryGet(First, out _));

        var faulting = new TaskCompletionSource<Receipt>();
        registry.Register(Second, faulting.Task);
        registry.Fail(Second, faulting, new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => faulting.Task);
        Assert.False(registry.TryGet(Second, out _));

        var again = new TaskCompletionSource<Receipt>();
        registry.Register(First, again.Task);
        Assert.True(registry.TryGet(First, out var second));
        Assert.Same(again.Task, second);
    }

    // Catches: a second registration silently replacing the first confirm's entry, so its joiners lose the shared outcome (AC-10, DEC-5)
    [Fact]
    public void Register_twice_for_one_reference_throws()
    {
        var registry = new InFlightPayments();
        var first = new TaskCompletionSource<Receipt>();
        registry.Register(First, first.Task);

        Assert.Throws<InvalidOperationException>(() => registry.Register(First, new TaskCompletionSource<Receipt>().Task));

        Assert.True(registry.TryGet(First, out var found));
        Assert.Same(first.Task, found);
    }

    // Catches: the entry still registered when the owner's task completes (BUG-006); the observer runs synchronously inside the completion
    [Fact]
    public void Entry_is_gone_when_the_awaited_task_completes()
    {
        var registry = new InFlightPayments();
        var gate = new TaskCompletionSource<Receipt>();
        var seen = true;
        _ = gate.Task.ContinueWith(
            completed => seen = registry.TryGet(First, out Task<Receipt>? ignored),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        registry.Register(First, gate.Task);

        registry.Complete(First, gate, ReceiptFor(First));

        Assert.False(seen);
    }

    // Catches: the entry still registered when the owner's task faults (BUG-006)
    [Fact]
    public void Entry_is_gone_when_the_awaited_task_faults()
    {
        var registry = new InFlightPayments();
        var gate = new TaskCompletionSource<Receipt>();
        var seen = true;
        _ = gate.Task.ContinueWith(
            completed => seen = registry.TryGet(First, out Task<Receipt>? ignored),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        registry.Register(First, gate.Task);

        registry.Fail(First, gate, new InvalidOperationException("boom"));

        Assert.False(seen);
    }
}
