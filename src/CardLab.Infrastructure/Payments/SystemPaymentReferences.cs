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
    /// <inheritdoc />
    public PaymentReference Next() => throw new NotImplementedException("PR-05");
}
