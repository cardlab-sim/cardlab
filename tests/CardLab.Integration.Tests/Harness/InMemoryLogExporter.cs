using System.Globalization;
using CardLab.TestKit;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace CardLab.Integration.Tests.Harness;

/// <summary>One exported log record, copied out of the pooled <see cref="LogRecord"/>.</summary>
/// <param name="EventName">Event name, e.g. "AuthorisationDecided".</param>
/// <param name="Attributes">Structured attributes by name (e.g. "CardId"), excluding "{OriginalFormat}".</param>
/// <param name="FormattedMessage">Formatted message, if any.</param>
/// <param name="Exception">Exception text (<c>Exception.ToString()</c>), if any; scanned by <see cref="InMemoryLogExporter.AssertNoPan"/>.</param>
public sealed record CapturedRecord(string? EventName, IReadOnlyDictionary<string, object?> Attributes, string? FormattedMessage, string? Exception = null);

/// <summary>
/// Captures OpenTelemetry log records for assertions (DEC-12, AC-24, AC-39). <see cref="LogRecord"/> instances are pooled:
/// copy every field inside <see cref="Export"/>. Thread-safe.
/// </summary>
public sealed class InMemoryLogExporter : BaseExporter<LogRecord>
{
    private readonly object _gate = new();
    private readonly List<CapturedRecord> _records = [];

    /// <summary>All captured records in export order.</summary>
    public IReadOnlyList<CapturedRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToList();
            }
        }
    }

    /// <summary>Records with an event name.</summary>
    /// <param name="eventName">Event name.</param>
    /// <returns>Matching records.</returns>
    public IReadOnlyList<CapturedRecord> All(string eventName) => Records.Where(r => r.EventName == eventName).ToList();

    /// <summary>The only record with an event name.</summary>
    /// <param name="eventName">Event name.</param>
    /// <returns>The record.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">When there is not exactly one.</exception>
    public CapturedRecord Single(string eventName)
    {
        var matches = All(eventName);
        return matches.Count == 1
            ? matches[0]
            : throw new Xunit.Sdk.XunitException($"Expected exactly one '{eventName}' log record but found {matches.Count}.");
    }

    /// <summary>Asserts no record (message or attribute values) contains 16 consecutive digits (uses <c>PanScanner</c>).</summary>
    public void AssertNoPan()
    {
        foreach (var record in Records)
        {
            PanScanner.AssertNoPan(record.FormattedMessage, $"log message of '{record.EventName}'");
            PanScanner.AssertNoPan(record.Exception, $"exception of '{record.EventName}'");
            foreach (var (name, value) in record.Attributes)
            {
                PanScanner.AssertNoPan(Convert.ToString(value, CultureInfo.InvariantCulture), $"log attribute '{name}' of '{record.EventName}'");
            }
        }
    }

    /// <summary>Forgets captured records (call at the start of a test that counts events).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _records.Clear();
        }
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            var attributes = new Dictionary<string, object?>();
            if (record.Attributes is not null)
            {
                foreach (var (key, value) in record.Attributes)
                {
                    if (key != "{OriginalFormat}")
                    {
                        attributes[key] = value;
                    }
                }
            }

            var captured = new CapturedRecord(record.EventId.Name, attributes, record.FormattedMessage, record.Exception?.ToString());
            lock (_gate)
            {
                _records.Add(captured);
            }
        }

        return ExportResult.Success;
    }
}
