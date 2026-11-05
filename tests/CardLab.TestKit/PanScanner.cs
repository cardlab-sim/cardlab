using System.Text.RegularExpressions;

namespace CardLab.TestKit;

/// <summary>Finds full card numbers (16 consecutive ASCII digits) in text (NFR-1, AC-24). Implemented in PR-01.</summary>
public static partial class PanScanner
{
    /// <summary>Finds every run of 16 or more consecutive digits.</summary>
    /// <param name="text">Any text (log line, JSON body, table dump, page text); null is treated as empty.</param>
    /// <returns>The offending runs (empty when clean).</returns>
    public static IReadOnlyList<string> FindPans(string? text) =>
        text is null ? [] : DigitRun().Matches(text).Select(m => m.Value).ToList();

    /// <summary>Asserts the text holds no full card number.</summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="where">Label for the failure message, e.g. "audit rows".</param>
    /// <exception cref="Xunit.Sdk.XunitException">When a 16-digit run is found (message shows only its last four).</exception>
    public static void AssertNoPan(string? text, string where)
    {
        var hits = FindPans(text);
        if (hits.Count > 0)
        {
            var lastFours = string.Join(", ", hits.Select(h => "..." + h[^4..]));
            throw new Xunit.Sdk.XunitException($"Full card number(s) found in {where}: {hits.Count} hit(s) ending {lastFours}.");
        }
    }

    // [0-9], not \d: \d also matches non-ASCII digits.
    [GeneratedRegex("[0-9]{16,}")]
    private static partial Regex DigitRun();
}
