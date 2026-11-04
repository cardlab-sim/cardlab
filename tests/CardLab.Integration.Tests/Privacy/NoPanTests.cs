using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Privacy;

[Collection("sql")]
public sealed class NoPanTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NoPanTests(ApiFactory factory) => _factory = factory;

    // T-I-7 · PR-11 · integration
    // Given J-1..J-3 run through the API (freeze, authorise frozen, unfreeze, set/change/remove limits, approved and limit_exceeded authorisations, an unknown-card request, malformed requests that put a 16-digit number in the category and merchant fields), When every raw response body, every captured log record (Logs), Database.DumpAllRowsAsync() and every GET list are scanned with PanScanner, Then no 16-digit run appears anywhere and the last four do appear in card responses
    // Catches: PAN leaking via responses, logs, ProblemDetails, stored rows (AC-24, NFR-1)
    [Fact(Skip = "todo")]
    public void No_full_card_number_anywhere_after_the_journeys()
    {
    }
}
