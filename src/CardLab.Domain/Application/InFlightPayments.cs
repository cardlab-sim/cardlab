using System.Diagnostics.CodeAnalysis;
using CardLab.Domain.Payments;

namespace CardLab.Domain.Application;

/// <summary>
/// Registry of confirms whose bank call is in flight (DEC-5): a second confirm of the same reference awaits the first
/// one's task instead of returning a half-known outcome (AC-10). Singleton; single-host assumption (ADR-001).
/// </summary>
/// <remarks>Thread-safe. The owner registers its task inside the first card lock, before the payment is committed, and
/// <see cref="Complete"/> / <see cref="Fail"/> remove the entry before completing the task, so a caller that has seen the
/// task complete never finds it again and a later confirm reads the stored receipt (BUG-006).</remarks>
public sealed class InFlightPayments
{
    private readonly object _gate = new();
    private readonly Dictionary<PaymentReference, Task<Receipt>> _tasks = [];

    /// <summary>Registers the in-flight task of a confirm; its owner later calls <see cref="Complete"/> or <see cref="Fail"/>.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="task">The owner's task, the one every concurrent caller for this reference awaits.</param>
    /// <exception cref="InvalidOperationException">The reference is already registered.</exception>
    public void Register(PaymentReference reference, Task<Receipt> task)
    {
        lock (_gate)
        {
            if (!_tasks.TryAdd(reference, task))
            {
                throw new InvalidOperationException("A confirm of " + reference + " is already in flight.");
            }
        }
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

    /// <summary>Removes the entry, then completes the owner's task with a result (BUG-006): no caller can observe the
    /// completion while the entry is still registered.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="completion">The task source whose task was handed to <see cref="Register"/>.</param>
    /// <param name="result">The receipt every joiner gets.</param>
    public void Complete(PaymentReference reference, TaskCompletionSource<Receipt> completion, Receipt result)
    {
        Release(reference);
        completion.SetResult(result);
    }

    /// <summary>Removes the entry, then faults the owner's task (BUG-006); a no-op for the task if it already completed.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="completion">The task source whose task was handed to <see cref="Register"/>.</param>
    /// <param name="error">The failure joiners see.</param>
    public void Fail(PaymentReference reference, TaskCompletionSource<Receipt> completion, Exception error)
    {
        Release(reference);
        completion.TrySetException(error);
    }

    private void Release(PaymentReference reference)
    {
        lock (_gate)
        {
            _tasks.Remove(reference);
        }
    }
}
