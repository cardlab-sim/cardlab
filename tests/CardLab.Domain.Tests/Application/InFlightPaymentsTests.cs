namespace CardLab.Domain.Tests.Application;

public sealed class InFlightPaymentsTests
{
    // T-U-30 · PR-04 · unit (application)
    // Given two concurrent GetOrAdd calls for one reference with a start function gated on a TaskCompletionSource, When both run, Then start ran once and both got the same task; TryGet is true while it runs and false after it completes (also after it faults); another reference is independent
    // Catches: the registry starting two bank calls or leaking entries (AC-10, DEC-5)
    [Fact(Skip = "todo")]
    public void Registry_starts_once_and_forgets_when_done()
    {
    }
}
