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
    // Given two concurrent GetOrAdd calls for one reference with a start function gated on a TaskCompletionSource, When both run, Then start ran once and both got the same task; TryGet is true while it runs and false after it completes (also after it faults); another reference is independent
    // Catches: the registry starting two bank calls or leaking entries (AC-10, DEC-5)
    [Fact]
    public async Task Registry_starts_once_and_forgets_when_done()
    {
        var registry = new InFlightPayments();
        var gate = new TaskCompletionSource<Receipt>();
        var starts = 0;

        var a = registry.GetOrAdd(First, () =>
        {
            starts++;
            return gate.Task;
        });
        var b = registry.GetOrAdd(First, () =>
        {
            starts++;
            return gate.Task;
        });

        Assert.Same(a, b);
        Assert.Equal(1, starts);
        Assert.True(registry.TryGet(First, out var found));
        Assert.Same(a, found);
        Assert.False(registry.TryGet(Second, out _));

        gate.SetResult(ReceiptFor(First));
        var receipt = await a;
        Assert.Equal(First, receipt.Reference);
        Assert.False(registry.TryGet(First, out _));

        var faulting = registry.GetOrAdd(Second, () => Task.FromException<Receipt>(new InvalidOperationException("boom")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => faulting);
        Assert.False(registry.TryGet(Second, out _));

        var again = registry.GetOrAdd(First, () => Task.FromResult(ReceiptFor(First)));
        Assert.NotSame(a, again);
        await again;
    }

    // Catches: the entry still registered after the start task completed, because removal ran in a later continuation (BUG-006)
    [Fact]
    public async Task Entry_is_gone_when_the_start_task_completes()
    {
        var registry = new InFlightPayments();
        var gate = new TaskCompletionSource<Receipt>(TaskCreationOptions.RunContinuationsAsynchronously);

        // A caller observing the completion first (continuation attached before the registry's own) must not see the entry.
        var seenRegistered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = gate.Task.ContinueWith(
            completed => seenRegistered.SetResult(registry.TryGet(First, out _)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        _ = registry.GetOrAdd(First, () => gate.Task);

        registry.Release(First);
        gate.SetResult(ReceiptFor(First));

        Assert.False(await seenRegistered.Task);
    }

    // Catches: a late cleanup of an old registration removing the entry of a newer one for the same reference (DEC-5)
    [Fact]
    public async Task Late_cleanup_of_a_released_registration_keeps_a_newer_entry()
    {
        var registry = new InFlightPayments();
        var oldGate = new TaskCompletionSource<Receipt>();
        _ = registry.GetOrAdd(First, () => oldGate.Task);
        registry.Release(First);
        var newGate = new TaskCompletionSource<Receipt>();
        var newer = registry.GetOrAdd(First, () => newGate.Task);

        oldGate.SetResult(ReceiptFor(First));
        await oldGate.Task;
        await Task.Yield();

        Assert.True(registry.TryGet(First, out var found));
        Assert.Same(newer, found);
        newGate.SetResult(ReceiptFor(First));
        await newer;
    }
}
