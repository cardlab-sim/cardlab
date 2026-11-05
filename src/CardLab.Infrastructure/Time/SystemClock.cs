using CardLab.Domain.Ports;

namespace CardLab.Infrastructure.Time;

/// <summary>The real clock (<c>DateTimeOffset.UtcNow</c>).</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
