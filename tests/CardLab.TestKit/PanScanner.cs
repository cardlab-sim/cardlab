namespace CardLab.TestKit;

/// <summary>Finds full card numbers (16 consecutive ASCII digits) in text (NFR-1, AC-24). Implemented in PR-01.</summary>
public static class PanScanner
{
    /// <summary>Finds every run of 16 or more consecutive digits.</summary>
    /// <param name="text">Any text (log line, JSON body, table dump, page text); null is treated as empty.</param>
    /// <returns>The offending runs (empty when clean).</returns>
    public static IReadOnlyList<string> FindPans(string? text) => throw new NotImplementedException("PR-01");

    /// <summary>Asserts the text holds no full card number.</summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="where">Label for the failure message, e.g. "audit rows".</param>
    /// <exception cref="Xunit.Sdk.XunitException">When a 16-digit run is found (message shows only its last four).</exception>
    public static void AssertNoPan(string? text, string where) => throw new NotImplementedException("PR-01");
}
