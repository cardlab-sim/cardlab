using CardLab.Domain.Ports;

namespace CardLab.Infrastructure.Time;

/// <summary>The real clock (<c>DateTimeOffset.UtcNow</c>).</summary>
/// <remarks>Implemented in PR-06.</remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => throw new NotImplementedException("PR-06");
}
