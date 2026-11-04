using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CardLab.Integration.Tests.Harness;

/// <summary>One exported log record, copied out of the pooled <see cref="LogRecord"/>.</summary>
/// <param name="EventName">Event name, e.g. "AuthorisationDecided".</param>
/// <param name="Attributes">Structured attributes by name (e.g. "CardId"), excluding "{OriginalFormat}".</param>
/// <param name="FormattedMessage">Formatted message, if any.</param>
public sealed record CapturedRecord(string? EventName, IReadOnlyDictionary<string, object?> Attributes, string? FormattedMessage);

/// <summary>
/// Captures OpenTelemetry log records for assertions (DEC-12, AC-24, AC-39). <see cref="LogRecord"/> instances are pooled:
/// copy every field inside <see cref="Export"/>. Thread-safe. Implemented in PR-06.
/// </summary>
public sealed class InMemoryLogExporter : BaseExporter<LogRecord>
{
    /// <summary>All captured records in export order.</summary>
    public IReadOnlyList<CapturedRecord> Records => throw new NotImplementedException("PR-06");

    /// <summary>Records with an event name.</summary>
    /// <param name="eventName">Event name.</param>
    /// <returns>Matching records.</returns>
    public IReadOnlyList<CapturedRecord> All(string eventName) => throw new NotImplementedException("PR-06");

    /// <summary>The only record with an event name.</summary>
    /// <param name="eventName">Event name.</param>
    /// <returns>The record.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">When there is not exactly one.</exception>
    public CapturedRecord Single(string eventName) => throw new NotImplementedException("PR-06");

    /// <summary>Asserts no record (message or attribute values) contains 16 consecutive digits (uses <c>PanScanner</c>).</summary>
    public void AssertNoPan() => throw new NotImplementedException("PR-06");

    /// <summary>Forgets captured records (call at the start of a test that counts events).</summary>
    public void Clear() => throw new NotImplementedException("PR-06");

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch) => throw new NotImplementedException("PR-06");
}
