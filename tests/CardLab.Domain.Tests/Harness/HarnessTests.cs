using CardLab.Domain.Ports;
using CardLab.TestKit;

namespace CardLab.Domain.Tests.Harness;

public sealed class HarnessTests
{
    // T-U-24 · PR-01 · unit
    // Given 32 bytes, When LookupHash.FromBytes then ToBytes, Then the bytes round-trip and Hex is 64 lower-case chars; 31 bytes throws ArgumentException
    // Catches: hash equality by reference / case, breaking FindByLookupAsync
    [Fact]
    public void LookupHash_round_trips_32_bytes()
    {
        var bytes = new byte[32];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(0xA0 + i);
        }

        var hash = LookupHash.FromBytes(bytes);

        Assert.Equal("a0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebf", hash.Hex);
        Assert.Equal(bytes, hash.ToBytes());
        Assert.Equal(hash, LookupHash.FromBytes(bytes));
        Assert.Throws<ArgumentException>(() => LookupHash.FromBytes(new byte[31]));
        Assert.Throws<ArgumentException>(() => LookupHash.FromBytes(new byte[33]));
    }

    // T-U-25 · PR-01 · unit
    // Given "card 9999000100000001 x", "…0001", "9999 0001 0000 0001" and a 17-digit run, When PanScanner.FindPans, Then the 16- and 17-digit runs are found, the last-four-only and space-separated texts are clean
    // Catches: a scanner that misses PANs (false green on AC-24) or flags last-four text
    [Fact]
    public void PanScanner_finds_runs_of_sixteen_digits()
    {
        Assert.Equal(["9999000100000001"], PanScanner.FindPans("card 9999000100000001 x"));
        Assert.Equal(["99990001000000012"], PanScanner.FindPans("n=99990001000000012."));
        Assert.Empty(PanScanner.FindPans("card ending 0001"));
        Assert.Empty(PanScanner.FindPans("9999 0001 0000 0001"));
        Assert.Empty(PanScanner.FindPans("999900010000001"));
        Assert.Empty(PanScanner.FindPans(null));
        Assert.Empty(PanScanner.FindPans("٩٩٩٩٩٩٩٩٩٩٩٩٩٩٩٩"));

        var failure = Assert.Throws<Xunit.Sdk.XunitException>(() => PanScanner.AssertNoPan("x 9999000100000001 y", "audit rows"));
        Assert.Contains("audit rows", failure.Message);
        Assert.Contains("0001", failure.Message);
        Assert.DoesNotContain("9999000100000001", failure.Message);
        PanScanner.AssertNoPan("clean 0001", "anything");
    }
}
