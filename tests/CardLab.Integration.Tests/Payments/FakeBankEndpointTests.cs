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
    [Fact(Skip = "todo")]
    public void Steering_endpoint_reads_merges_and_validates()
    {
    }
}
