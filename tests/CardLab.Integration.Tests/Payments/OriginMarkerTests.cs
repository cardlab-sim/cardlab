using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class OriginMarkerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public OriginMarkerTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-18 · PR-08 · integration
    // Given a quote with header X-CardLab-Origin: deploy-smoke and quotes with "Deploy-Smoke", "x" and no header, When each is confirmed, Then the first's quote, receipt, record (acceptance.origin), payments list item and activity row say deploy_smoke and all others cardholder_view
    // Catches: smoke payments indistinguishable from real use, or header variants spoofing it (DEC-15)
    [Theory]
    [InlineData("deploy-smoke", "deploy_smoke")]
    [InlineData("Deploy-Smoke", "cardholder_view")]
    [InlineData("x", "cardholder_view")]
    [InlineData(null, "cardholder_view")]
    public async Task Only_the_exact_header_marks_a_smoke_payment(string? header, string expected)
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("12.00", "fuel", "Shell", null));

        var quote = await client.CreateQuoteAsync(id, header);
        Assert.Equal(HttpStatusCode.Created, quote.Status);
        var reference = quote.Body!.Reference;
        var confirmed = await client.ConfirmAsync(id, reference);
        Assert.Equal("completed", confirmed.Body!.State);
        var receipt = await client.GetReceiptAsync(id, reference);
        var record = await client.GetPaymentRecordAsync(id, reference);
        var listed = (await client.ListPaymentsAsync(id)).Body!.Single(p => p.Reference == reference);
        var activity = (await client.ListActivityAsync(id)).Body!.Single(a => a.Reference == reference);

        Assert.Equal(expected, quote.Body.Origin);
        Assert.Equal(expected, confirmed.Body.Origin);
        Assert.Equal(expected, receipt.Body!.Origin);
        Assert.Equal(expected, record.Body!.Acceptance.Origin);
        Assert.Equal(expected, listed.Origin);
        Assert.Equal(expected, activity.Origin);
    }
}
