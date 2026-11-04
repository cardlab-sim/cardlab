using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Api;

[Collection("sql")]
public sealed class AuthorisationEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthorisationEndpointTests(ApiFactory factory) => _factory = factory;

    // T-I-2 · PR-08 · integration
    // Given Field sales frozen (Database.SetStatusAsync) with no limits, When POST /api/authorisations £1.00 fuel, Then 200 {outcome declined, reason card_frozen, transactionId set}, the transaction is first in the card's stored history (ICardStore.ListTransactionsAsync from a DI scope) as declined, and approved Fuel spend is unchanged
    // Catches: frozen decision not stored or counted as spend (AC-4)
    [Fact(Skip = "todo")]
    public void Frozen_card_is_declined_and_stored()
    {
    }

    // T-I-2 · PR-08 · integration
    // Given Field sales frozen and then set Active again, When POST /api/authorisations within limits, Then 200 approved
    // Catches: status read from a stale cache (AC-5)
    [Fact(Skip = "todo")]
    public void Unfrozen_card_is_approved()
    {
    }

    // T-I-4 · PR-08 · integration
    // Given Ops van Fuel/Day £50.00 and £30.00 approved today (London), When £20.00, £20.01 then £20.00 fuel arrive that day, Then approved remaining "0.00"; then (fresh class state: £30.00) declined limit_exceeded remaining "20.00" stored as declined; then approved
    // Catches: SQL spend sum including declines or wrong period range (AC-10, AC-11, AC-12)
    [Fact(Skip = "todo")]
    public void Limit_arithmetic_end_to_end()
    {
    }

    // T-I-5 · PR-08 · integration
    // Given Seed.UnknownCardNumber, When POST /api/authorisations, Then 404 {declined, card_not_found, transactionId null, limits []} and Database.CountTransactionsAsync unchanged
    // Catches: unknown card stored or 200 (AC-19, DEC-14)
    [Fact(Skip = "todo")]
    public void Unknown_card_is_404_and_stores_nothing()
    {
    }

    // T-I-5 · PR-08 · integration (theory)
    // Given requests with amount missing, "0.00", category "petrol", timestamp "not-a-time", When POST /api/authorisations, Then 400 {declined, invalid_request, problems:[{field,…}]} and no transaction stored
    // Catches: malformed requests stored, or framework 400s without the decision body (AC-20)
    [Fact(Skip = "todo")]
    public void Malformed_requests_are_400_and_store_nothing()
    {
    }

    // T-I-9 · PR-08 · integration
    // Given Marketing Fuel/Day £50.00, When two identical £30.00 fuel requests (same merchant and timestamp) arrive one after the other, Then approved then declined limit_exceeded, both in the history
    // Catches: accidental idempotency/deduplication (AC-32)
    [Fact(Skip = "todo")]
    public void Repeated_identical_request_is_a_second_transaction()
    {
    }
}
