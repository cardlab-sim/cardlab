using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class OriginMarkerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public OriginMarkerTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-18 · PR-08 · integration
    // Given a quote with header X-CardLab-Origin: deploy-smoke and quotes with "Deploy-Smoke", "x" and no header, When each is confirmed, Then the first's quote, receipt, record (acceptance.origin), payments list item and activity row say deploy_smoke and all others cardholder_view
    // Catches: smoke payments indistinguishable from real use, or header variants spoofing it (DEC-15)
    [Fact(Skip = "todo")]
    public void Only_the_exact_header_marks_a_smoke_payment()
    {
    }
}
