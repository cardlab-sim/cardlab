using System.Security.Cryptography;
using System.Text;
using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using CardLab.TestKit.Builders;
using Microsoft.Extensions.Logging;

namespace CardLab.TestKit.Fakes;

/// <summary>A controllable clock for unit tests. Implemented in PR-01.</summary>
public sealed class FakeClock : IClock
{
    private DateTimeOffset now;

    /// <summary>Creates the clock at an instant.</summary>
    /// <param name="start">Initial <see cref="UtcNow"/> (converted to offset zero).</param>
    public FakeClock(DateTimeOffset start) => now = start.ToUniversalTime();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => now;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">Duration (non-negative).</param>
    public void Advance(TimeSpan by) => now += by;

    /// <summary>Sets the clock.</summary>
    /// <param name="now">New instant.</param>
    public void Set(DateTimeOffset now) => this.now = now.ToUniversalTime();
}

/// <summary>Unkeyed SHA-256 of the digits: deterministic, no configuration (unit tests only). Implemented in PR-01.</summary>
public sealed class FakeCardNumberLookup : ICardNumberLookup
{
    private int calls;

    /// <summary>Shared instance. Shared across parallel tests; never assert <see cref="Calls"/> on it: create <c>new FakeCardNumberLookup()</c> per test to count calls.</summary>
    public static FakeCardNumberLookup Instance { get; } = new();

    /// <summary>Number of <see cref="Compute"/> calls (lets tests assert "no lookup for invalid requests").</summary>
    public int Calls => Volatile.Read(ref calls);

    /// <inheritdoc />
    public LookupHash Compute(string digits)
    {
        Interlocked.Increment(ref calls);
        return LookupHash.FromBytes(SHA256.HashData(Encoding.ASCII.GetBytes(digits)));
    }
}

/// <summary>A fixed actor. Implemented in PR-01.</summary>
public sealed class FakeActor : ICurrentActor
{
    private readonly Actor actor;

    /// <summary>Creates the fake.</summary>
    /// <param name="actor">The actor to return.</param>
    public FakeActor(Actor actor) => this.actor = actor;

    /// <summary>The seeded admin, Sam Ellis.</summary>
    public static FakeActor Sam => new(ActorBuilder.Sam);

    /// <inheritdoc />
    public Actor Current => actor;
}

/// <summary>One captured log entry.</summary>
/// <param name="Level">Level.</param>
/// <param name="EventName">Event name, e.g. "AuthorisationDecided".</param>
/// <param name="Message">Formatted message.</param>
/// <param name="Fields">Structured fields by placeholder name (e.g. "CardId"), excluding "{OriginalFormat}".</param>
public sealed record CapturedLog(LogLevel Level, string? EventName, string Message, IReadOnlyDictionary<string, object?> Fields);

/// <summary>An <see cref="ILogger{T}"/> that records every entry for assertions. Implemented in PR-01.</summary>
/// <typeparam name="T">Category type.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly object gate = new();
    private readonly List<CapturedLog> entries = [];

    /// <summary>Entries in write order.</summary>
    public IReadOnlyList<CapturedLog> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToArray();
            }
        }
    }

    /// <summary>Entries with the given event name.</summary>
    /// <param name="eventName">e.g. "ControlChanged".</param>
    /// <returns>Matching entries in write order.</returns>
    public IReadOnlyList<CapturedLog> Events(string eventName) =>
        Entries.Where(e => e.EventName == eventName).ToArray();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NoopScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = new Dictionary<string, object?>();
        if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair.Key != "{OriginalFormat}")
                {
                    fields[pair.Key] = pair.Value;
                }
            }
        }

        var entry = new CapturedLog(logLevel, eventId.Name, formatter(state, exception), fields);
        lock (gate)
        {
            entries.Add(entry);
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
