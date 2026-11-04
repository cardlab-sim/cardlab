using CardLab.Domain.Controls;

namespace CardLab.Domain.Ports;

/// <summary>Source of the current time, so domain and service code never read the system clock (design §15.1).</summary>
public interface IClock
{
    /// <summary>Current instant, UTC (offset zero).</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>One-way, keyed lookup value of a card number (DEC-8). The number itself is never stored.</summary>
public interface ICardNumberLookup
{
    /// <summary>Computes the lookup value of a card number.</summary>
    /// <param name="digits">Exactly 16 ASCII digits (already validated).</param>
    /// <returns>The 32-byte lookup hash; equal digits give equal hashes.</returns>
    LookupHash Compute(string digits);
}

/// <summary>Who is acting. In this feature always the seeded admin (requirements decision 4).</summary>
public interface ICurrentActor
{
    /// <summary>The acting admin.</summary>
    Actor Current { get; }
}

/// <summary>
/// Thrown by <see cref="ICardStore.WithCardLockAsync{T}"/> when the per-card lock is not granted within
/// 5 s (DEC-4); nothing was stored. The API maps it to <c>503</c> ProblemDetails (design §9).
/// </summary>
public sealed class CardLockTimeoutException : Exception
{
    /// <summary>Creates the exception.</summary>
    public CardLockTimeoutException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What timed out (card id only, never a card number).</param>
    public CardLockTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and cause.</summary>
    /// <param name="message">What timed out.</param>
    /// <param name="innerException">Underlying error.</param>
    public CardLockTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
