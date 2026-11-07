using System.Security.Cryptography;
using System.Text;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Lookup;

/// <summary><c>HMAC-SHA256(key, digits)</c> card lookup (DEC-8, ADR-003). The key never appears in logs.</summary>
public sealed class HmacCardNumberLookup : ICardNumberLookup
{
    private readonly IOptions<LookupOptions> _options;

    /// <summary>Creates the lookup.</summary>
    /// <param name="options">Holds <c>CardLab:LookupKey</c> (base64 of 32 bytes).</param>
    public HmacCardNumberLookup(IOptions<LookupOptions> options) => _options = options;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The key is missing, not base64, or not 32 bytes (message never contains the key).</exception>
    public LookupHash Compute(string digits)
    {
        var key = ReadKey(_options.Value.LookupKey);
        return LookupHash.FromBytes(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(digits)));
    }

    private static byte[] ReadKey(string? base64)
    {
        const string Problem = "CardLab:LookupKey must be set to the base64 encoding of exactly 32 bytes.";
        if (string.IsNullOrEmpty(base64))
        {
            throw new InvalidOperationException(Problem);
        }

        try
        {
            var key = Convert.FromBase64String(base64);
            return key.Length == 32 ? key : throw new InvalidOperationException(Problem);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(Problem);
        }
    }
}
