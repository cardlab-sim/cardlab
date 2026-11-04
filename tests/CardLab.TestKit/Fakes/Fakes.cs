using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace CardLab.TestKit.Fakes;

/// <summary>A controllable clock for unit tests. Implemented in PR-01.</summary>
public sealed class FakeClock : IClock
{
    /// <summary>Creates the clock at an instant.</summary>
    /// <param name="start">Initial <see cref="UtcNow"/> (converted to offset zero).</param>
    public FakeClock(DateTimeOffset start) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public DateTimeOffset UtcNow => throw new NotImplementedException("PR-01");

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">Duration (non-negative).</param>
    public void Advance(TimeSpan by) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the clock.</summary>
    /// <param name="now">New instant.</param>
    public void Set(DateTimeOffset now) => throw new NotImplementedException("PR-01");
}

/// <summary>Unkeyed SHA-256 of the digits: deterministic, no configuration (unit tests only). Implemented in PR-01.</summary>
public sealed class FakeCardNumberLookup : ICardNumberLookup
{
    /// <summary>Shared instance.</summary>
    public static FakeCardNumberLookup Instance { get; } = new();

    /// <summary>Number of <see cref="Compute"/> calls (lets tests assert "no lookup for invalid requests").</summary>
    public int Calls => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public LookupHash Compute(string digits) => throw new NotImplementedException("PR-01");
}

/// <summary>A fixed actor. Implemented in PR-01.</summary>
public sealed class FakeActor : ICurrentActor
{
    /// <summary>Creates the fake.</summary>
    /// <param name="actor">The actor to return.</param>
    public FakeActor(Actor actor) => throw new NotImplementedException("PR-01");

    /// <summary>The seeded admin, Sam Ellis.</summary>
    public static FakeActor Sam => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public Actor Current => throw new NotImplementedException("PR-01");
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
    /// <summary>Entries in write order.</summary>
    public IReadOnlyList<CapturedLog> Entries => throw new NotImplementedException("PR-01");

    /// <summary>Entries with the given event name.</summary>
    /// <param name="eventName">e.g. "ControlChanged".</param>
    /// <returns>Matching entries in write order.</returns>
    public IReadOnlyList<CapturedLog> Events(string eventName) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => throw new NotImplementedException("PR-01");

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new NotImplementedException("PR-01");
}
