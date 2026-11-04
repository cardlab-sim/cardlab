namespace CardLab.Domain.Tests.Harness;

public sealed class HarnessTests
{
    // T-U-24 · PR-01 · unit
    // Given 32 bytes, When LookupHash.FromBytes then ToBytes, Then the bytes round-trip and Hex is 64 lower-case chars; 31 bytes throws ArgumentException
    // Catches: hash equality by reference / case, breaking FindByLookupAsync
    [Fact(Skip = "todo")]
    public void LookupHash_round_trips_32_bytes()
    {
    }

    // T-U-25 · PR-01 · unit
    // Given "card 9999000100000001 x", "…0001", "9999 0001 0000 0001" and a 17-digit run, When PanScanner.FindPans, Then the 16- and 17-digit runs are found, the last-four-only and space-separated texts are clean
    // Catches: a scanner that misses PANs (false green on AC-24) or flags last-four text
    [Fact(Skip = "todo")]
    public void PanScanner_finds_runs_of_sixteen_digits()
    {
    }
}
