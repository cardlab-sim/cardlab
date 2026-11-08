using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class LimitsUnaffectedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public LimitsUnaffectedTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-9 · PR-06 · integration
    // Given Ops van with limit Fuel/day £50.00, £40.00 Fuel approved today and a completed £40.00 payment, When a £20.00 Fuel purchase is authorised today, Then declined limit_exceeded
    // Catches: payments reducing category spend (AC-19, R-13)
    [Fact]
    public async Task Payments_do_not_reduce_category_spend()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        Assert.Equal(HttpStatusCode.OK, (await client.SetLimitAsync(id, "fuel", "day", "50.00")).Status);
        var approved = await client.SimulatePurchaseAsync(id, new SimulatedPurchase("40.00", "fuel", "Shell", null));
        Assert.Equal("approved", approved.Body!.Outcome);
        var reference = (await client.CreateQuoteAsync(id)).Body!.Reference;
        Assert.Equal("completed", (await client.ConfirmAsync(id, reference)).Body!.State);

        var purchase = await client.SimulatePurchaseAsync(id, new SimulatedPurchase("20.00", "fuel", "Shell", null));

        Assert.Equal("declined", purchase.Body!.Outcome);
        Assert.Equal("limit_exceeded", purchase.Body.Reason);
    }
}
