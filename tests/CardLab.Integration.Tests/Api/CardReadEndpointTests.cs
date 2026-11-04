using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class CardReadEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CardReadEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-19 · PR-09 · integration
    // Given the seed, When GET /api/cards and GET /api/cards/{id}, Then 3 cards ordered by name with {id, name, cardholderName, lastFour, status:"active"} and the detail with limits []; GET /api/cards/{unknown} → 404 ProblemDetails; no body contains a 16-digit run
    // Catches: wrong wire names/casing (§7), PAN in responses (NFR-1)
    [Fact(Skip = "todo")]
    public void Lists_and_reads_cards()
    {
    }

    // T-I-19 · PR-09 · integration
    // Given 2 transactions and 2 audit events seeded with increasing times, When GET transactions / audit, Then newest first with the §7 fields (amount "12.34", category wire name, outcome/reason wire names, occurredAt with its original offset); unknown card id → 404
    // Catches: oldest-first lists (AC-22, AC-23) or enum names leaking ("LimitExceeded")
    [Fact(Skip = "todo")]
    public void Lists_transactions_and_audit_newest_first()
    {
    }
}
