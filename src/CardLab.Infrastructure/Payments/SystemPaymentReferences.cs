using System.Security.Cryptography;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;

namespace CardLab.Infrastructure.Payments;

/// <summary>
/// Real reference source (DEC-8): <c>PAY-</c> + 8 Crockford base32 characters from 40 bits of
/// <c>System.Security.Cryptography.RandomNumberGenerator</c>; always matches <see cref="PaymentReference.Pattern"/>.
/// Thread-safe singleton. A collision is caught by the quote primary key (503, design §9). Implemented in PR-05.
/// </summary>
public sealed class SystemPaymentReferences : IPaymentReferences
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <inheritdoc />
    public PaymentReference Next()
    {
        Span<byte> bytes = stackalloc byte[5];
        RandomNumberGenerator.Fill(bytes);
        var bits = 0UL;
        foreach (var b in bytes)
        {
            bits = (bits << 8) | b;
        }

        Span<char> text = stackalloc char[12];
        "PAY-".CopyTo(text);
        for (var i = 11; i >= 4; i--)
        {
            text[i] = Alphabet[(int)(bits & 31)];
            bits >>= 5;
        }

        return new PaymentReference(new string(text));
    }
}
