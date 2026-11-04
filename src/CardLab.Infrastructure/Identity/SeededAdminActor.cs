using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Identity;

/// <summary>The seeded admin as the actor of every control change (no login in this feature; a later auth feature replaces this class only).</summary>
/// <remarks>Implemented in PR-06.</remarks>
public sealed class SeededAdminActor : ICurrentActor
{
    private readonly IOptions<SeedOptions> _options;

    /// <summary>Creates the actor.</summary>
    /// <param name="options">Seed options (<c>AdminId</c>, <c>AdminName</c>).</param>
    public SeededAdminActor(IOptions<SeedOptions> options) => _options = options;

    /// <inheritdoc />
    public Actor Current => throw new NotImplementedException("PR-06");
}
