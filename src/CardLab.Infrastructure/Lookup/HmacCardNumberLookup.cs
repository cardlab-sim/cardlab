using CardLab.Domain.Ports;
using CardLab.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Lookup;

/// <summary><c>HMAC-SHA256(key, digits)</c> card lookup (DEC-8, ADR-003). The key never appears in logs.</summary>
/// <remarks>Implemented in PR-06.</remarks>
public sealed class HmacCardNumberLookup : ICardNumberLookup
{
    private readonly IOptions<LookupOptions> _options;

    /// <summary>Creates the lookup.</summary>
    /// <param name="options">Holds <c>CardLab:LookupKey</c> (base64 of 32 bytes).</param>
    public HmacCardNumberLookup(IOptions<LookupOptions> options) => _options = options;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The key is missing, not base64, or not 32 bytes (message never contains the key).</exception>
    public LookupHash Compute(string digits) => throw new NotImplementedException("PR-06");
}
