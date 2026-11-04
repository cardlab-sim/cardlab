namespace CardLab.Integration.Tests.Lookup;

public sealed class HmacCardNumberLookupTests
{
    // T-I-16 · PR-06 · integration (no database)
    // Given the dev key, When Compute("9999000100000001") twice and with another key, Then equal hashes for the same key, different for another key, equal to HMACSHA256(key, ASCII digits); a missing, non-base64 or 16-byte key throws InvalidOperationException whose message contains neither the key nor the digits
    // Catches: unkeyed hashing (DEC-8), encoding mismatch with the seeder, secrets in error messages
    [Fact(Skip = "todo")]
    public void Hmac_is_keyed_deterministic_and_validates_the_key()
    {
    }
}
