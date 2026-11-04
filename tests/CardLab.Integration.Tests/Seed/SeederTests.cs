using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Seed;

[Collection("sql")]
public sealed class SeederTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SeederTests(ApiFactory factory) => _factory = factory;

    // T-I-13 · PR-06 · integration
    // Given the host started (migrate + seed ran) and Seeder.EnsureSeedAsync is run twice more, When the tables are read, Then exactly 1 account "Brightline Supplies Ltd" (Europe/London), 1 admin "Sam Ellis" with Seed.AdminId, 3 cards with the seeded names, cardholders, last four 0001/0002/0003, Active, and ICardStore.FindByLookupAsync(lookup of each seeded number) returns that card
    // Catches: duplicate rows on redeploy, missing seed, hash computed with a different key/format
    [Fact(Skip = "todo")]
    public void Seed_is_idempotent_and_cards_are_findable_by_number()
    {
    }

    // T-I-13 · PR-06 · integration
    // Given a card whose LookupHash was overwritten with zeros and which is Frozen with a limit, When EnsureSeedAsync runs, Then the hash is recomputed and status, limits and history are untouched
    // Catches: key rotation not healed (DEC-8), or reseed wiping state
    [Fact(Skip = "todo")]
    public void Reseed_recomputes_lookup_hash_and_keeps_state()
    {
    }
}
