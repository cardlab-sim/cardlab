using System.Diagnostics.CodeAnalysis;
using CardLab.Domain.Payments;

namespace CardLab.Domain.Application;

/// <summary>
/// Registry of confirms whose bank call is in flight (DEC-5): a second confirm of the same reference awaits the first
/// one's task instead of returning a half-known outcome (AC-10). Singleton; single-host assumption (ADR-001).
/// </summary>
/// <remarks>Implemented in PR-04. Thread-safe (<c>ConcurrentDictionary</c>); an entry is removed when its task
/// completes (successfully or not), so a later confirm reads the stored receipt.</remarks>
public sealed class InFlightPayments
{
    /// <summary>Returns the in-flight task for <paramref name="reference"/>, starting and registering <paramref name="start"/> when none exists.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="start">Starts the bank call and settlement; invoked at most once per registration.</param>
    /// <returns>The task every concurrent caller for this reference awaits.</returns>
    public Task<Receipt> GetOrAdd(PaymentReference reference, Func<Task<Receipt>> start) => throw new NotImplementedException("PR-04");

    /// <summary>Finds an in-flight task.</summary>
    /// <param name="reference">Payment reference.</param>
    /// <param name="inFlight">The task when the method returns true.</param>
    /// <returns>True while a confirm for the reference is between registration and completion.</returns>
    public bool TryGet(PaymentReference reference, [NotNullWhen(true)] out Task<Receipt>? inFlight) => throw new NotImplementedException("PR-04");
}
