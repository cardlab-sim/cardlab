using System.Net;
using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class FakeBankEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public FakeBankEndpointTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-21 · PR-06 · integration
    // Given a reset, When GET /api/fake-bank, Then {nextDebitOutcome complete, nextStatusCheck answers, account {lastFour 7731, availableBalance 25000.00}, entries []}; PUT {nextDebitOutcome refuse_insufficient_funds} → 200 with that value and nextStatusCheck unchanged; PUT {nextStatusCheck does_not_answer} keeps the debit setting; PUT {nextDebitOutcome "explode"} → 400 and nothing changed
    // Catches: the steering control unusable or accepting junk (R-15, DEC-6, DEC-7)
    [Fact]
    public async Task Steering_endpoint_reads_merges_and_validates()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();

        var initial = await client.GetFakeBankAsync();
        Assert.Equal(HttpStatusCode.OK, initial.Status);
        Assert.Equal("complete", initial.Body!.NextDebitOutcome);
        Assert.Equal("answers", initial.Body.NextStatusCheck);
        Assert.Equal("7731", initial.Body.Account.LastFour);
        Assert.Equal("25000.00", initial.Body.Account.AvailableBalance);
        Assert.Empty(initial.Body.Entries);

        var refuse = await client.SetSteeringAsync("refuse_insufficient_funds");
        Assert.Equal(HttpStatusCode.OK, refuse.Status);
        Assert.Equal("refuse_insufficient_funds", refuse.Body!.NextDebitOutcome);
        Assert.Equal("answers", refuse.Body.NextStatusCheck);

        var silent = await client.SetSteeringAsync(null, "does_not_answer");
        Assert.Equal(HttpStatusCode.OK, silent.Status);
        Assert.Equal("refuse_insufficient_funds", silent.Body!.NextDebitOutcome);
        Assert.Equal("does_not_answer", silent.Body.NextStatusCheck);

        var debitOnly = await client.SetSteeringAsync("complete");
        Assert.Equal("complete", debitOnly.Body!.NextDebitOutcome);
        Assert.Equal("does_not_answer", debitOnly.Body.NextStatusCheck);
        await client.SetSteeringAsync("refuse_insufficient_funds");

        var junk = await client.SetSteeringAsync("explode");
        Assert.Equal(HttpStatusCode.BadRequest, junk.Status);
        var after = await client.GetFakeBankAsync();
        Assert.Equal("refuse_insufficient_funds", after.Body!.NextDebitOutcome);
        Assert.Equal("does_not_answer", after.Body.NextStatusCheck);

        var junkCheck = await client.SetSteeringAsync("complete", "maybe");
        Assert.Equal(HttpStatusCode.BadRequest, junkCheck.Status);
        var unchanged = await client.GetFakeBankAsync();
        Assert.Equal("refuse_insufficient_funds", unchanged.Body!.NextDebitOutcome);
    }
}
