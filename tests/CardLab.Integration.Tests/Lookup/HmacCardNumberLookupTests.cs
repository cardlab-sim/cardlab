using System.Security.Cryptography;
using System.Text;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Lookup;
using CardLab.Infrastructure.Options;
using TestSeed = CardLab.TestKit.Seed;
using Microsoft.Extensions.Options;

namespace CardLab.Integration.Tests.Lookup;

public sealed class HmacCardNumberLookupTests
{
    private const string Digits = "9999000100000001";
    private const string OtherKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private static HmacCardNumberLookup LookupWith(string key) =>
        new(Options.Create(new LookupOptions { LookupKey = key }));

    // T-I-16 · PR-06 · integration (no database)
    // Given the dev key, When Compute("9999000100000001") twice and with another key, Then equal hashes for the same key, different for another key, equal to HMACSHA256(key, ASCII digits); a missing, non-base64 or 16-byte key throws InvalidOperationException whose message contains neither the key nor the digits
    // Catches: unkeyed hashing (DEC-8), encoding mismatch with the seeder, secrets in error messages
    [Fact]
    public void Hmac_is_keyed_deterministic_and_validates_the_key()
    {
        var lookup = LookupWith(TestSeed.DevLookupKey);

        var first = lookup.Compute(Digits);
        var second = lookup.Compute(Digits);
        var other = LookupWith(OtherKey).Compute(Digits);

        var expected = LookupHash.FromBytes(HMACSHA256.HashData(Convert.FromBase64String(TestSeed.DevLookupKey), Encoding.ASCII.GetBytes(Digits)));
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(expected, first);

        var sixteenByteKey = Convert.ToBase64String(new byte[16]);
        foreach (var badKey in new[] { "", "not base64 !!", sixteenByteKey })
        {
            var error = Assert.Throws<InvalidOperationException>(() => LookupWith(badKey).Compute(Digits));
            Assert.DoesNotContain(Digits, error.Message);
            if (badKey.Length > 0)
            {
                Assert.DoesNotContain(badKey, error.Message);
            }
        }
    }
}
