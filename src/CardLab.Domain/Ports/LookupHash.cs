namespace CardLab.Domain.Ports;

/// <summary>
/// The 32-byte keyed hash of a card number (DEC-8), carried as 64 lower-case hex characters so it has
/// value equality (usable as a dictionary key) and maps to <c>binary(32)</c> through
/// <see cref="FromBytes"/>/<see cref="ToBytes"/>.
/// </summary>
/// <param name="Hex">64 lower-case hex characters.</param>
public readonly record struct LookupHash(string Hex)
{
    /// <summary>Creates a hash from raw bytes.</summary>
    /// <param name="bytes">Exactly 32 bytes.</param>
    /// <returns>The hash with lower-case hex.</returns>
    /// <exception cref="ArgumentException">When <paramref name="bytes"/> is not 32 bytes long.</exception>
    public static LookupHash FromBytes(ReadOnlySpan<byte> bytes) =>
        bytes.Length == 32
            ? new LookupHash(Convert.ToHexStringLower(bytes))
            : throw new ArgumentException("A lookup hash is exactly 32 bytes.", nameof(bytes));

    /// <summary>The raw 32 bytes (for the <c>binary(32)</c> column).</summary>
    /// <returns>A new array of 32 bytes.</returns>
    public byte[] ToBytes() => Convert.FromHexString(Hex);
}
