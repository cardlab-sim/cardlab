using System.Diagnostics.CodeAnalysis;
using CardLab.Domain.Payments;

namespace CardLab.Domain.Application;

/// <summary>
/// Registry of confirms whose bank call is in flight (DEC-5): a second confirm of the same reference awaits the first
/// one's task instead of returning a half-known outcome (AC-10). Singleton; single-host assumption (ADR-001).
/// </summary>
/// <remarks>Thread-safe. An entry is removed just before its task completes (successfully or not), by the registry or earlier by <see cref="Release"/>, so a caller that
/// has seen the task complete never finds it again and a later confirm reads the stored receipt.</remarks>
public sealed class InFlightPayments
{
    private readonly object _gate = new();
    private readonly Dictionary<PaymentReference, Task<Receipt>> _tasks = [];

    /// <summary>Returns the in-flight task for <paramref name="reference"/>, starting and registering <paramref name="start"/> when none exists.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="start">Starts the bank call and settlement; invoked at most once per registration, after the entry is visible to <see cref="TryGet"/>.</param>
    /// <returns>The task every concurrent caller for this reference awaits.</returns>
    public Task<Receipt> GetOrAdd(PaymentReference reference, Func<Task<Receipt>> start)
    {
        TaskCompletionSource<Receipt> source;
        lock (_gate)
        {
            if (_tasks.TryGetValue(reference, out var existing))
            {
                return existing;
            }

            source = new TaskCompletionSource<Receipt>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tasks[reference] = source.Task;
        }

        _ = RunAsync(reference, start, source);
        return source.Task;
    }

    /// <summary>Finds an in-flight task.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="inFlight">The task when the method returns true.</param>
    /// <returns>True while a confirm for the reference is between registration and completion.</returns>
    public bool TryGet(PaymentReference reference, [NotNullWhen(true)] out Task<Receipt>? inFlight)
    {
        lock (_gate)
        {
            return _tasks.TryGetValue(reference, out inFlight);
        }
    }

    /// <summary>Removes the entry for <paramref name="reference"/> now (BUG-006): the owner of the start task calls this just
    /// before it completes that task, so no caller can observe the completion while the entry is still registered.</summary>
    /// <param name="reference">Payment reference.</param>
    public void Release(PaymentReference reference)
    {
        lock (_gate)
        {
            _tasks.Remove(reference);
        }
    }

    private async Task RunAsync(PaymentReference reference, Func<Task<Receipt>> start, TaskCompletionSource<Receipt> source)
    {
        try
        {
            var receipt = await start().ConfigureAwait(false);
            Remove(reference, source.Task);
            source.SetResult(receipt);
        }
        catch (Exception error)
        {
            Remove(reference, source.Task);
            source.SetException(error);
        }
    }

    // Only this registration's own entry: after a Release the reference may already belong to a newer registration.
    private void Remove(PaymentReference reference, Task<Receipt> own)
    {
        lock (_gate)
        {
            if (_tasks.TryGetValue(reference, out var current) && ReferenceEquals(current, own))
            {
                _tasks.Remove(reference);
            }
        }
    }
}
