namespace CardLab.Domain.Payments;

/// <summary>
/// The payment reference minted when the review screen is shown (R-7, DEC-8): <c>PAY-</c> followed by 8 Crockford
/// base32 characters, e.g. <c>PAY-7K3M9Q2T</c>. It is the quote's key, the exactly-once key and the bank's reference.
/// </summary>
/// <remarks>
/// Construct from trusted sources (the store, <c>IPaymentReferences</c>) with the constructor; parse untrusted text
/// (route values) with <see cref="TryParse"/>. Implemented in PR-01.
/// </remarks>
/// <param name="Value">The reference text, e.g. <c>PAY-00000001</c>.</param>
public readonly record struct PaymentReference(string Value)
{
    /// <summary>The format (design §10): upper-case Crockford alphabet (no I, L, O, U), exactly 8 characters.</summary>
    public const string Pattern = "^PAY-[0-9A-HJKMNP-TV-Z]{8}$";

    /// <summary>Parses a reference from untrusted text.</summary>
    /// <param name="text">Candidate text; null, lower case, padded or wrong-length text fails (no normalisation).</param>
    /// <param name="reference">The parsed reference when the method returns true; default otherwise.</param>
    /// <returns>True when <paramref name="text"/> matches <see cref="Pattern"/> exactly.</returns>
    public static bool TryParse(string? text, out PaymentReference reference) => throw new NotImplementedException("PR-01");

    /// <summary>The reference text.</summary>
    /// <returns><see cref="Value"/>.</returns>
    public override string ToString() => Value;
}
