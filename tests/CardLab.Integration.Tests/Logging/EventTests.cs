using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Logging;

[Collection("sql")]
public sealed class EventTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EventTests(ApiFactory factory) => _factory = factory;

    // T-I-11 · PR-11 · integration
    // Given Logs cleared, When one processor authorisation, one simulate purchase, one unknown-card and one invalid request are made, Then exactly four AuthorisationDecided records with CardId, LastFour, Category, Amount, Outcome, Reason, DurationMs (CardId/LastFour "-" for the last two), and no record contains a 16-digit run
    // Catches: endpoint-level double logging, missing fields, simulate path not logging (AC-39, NFR-7)
    [Fact(Skip = "todo")]
    public void One_authorisation_event_per_decision()
    {
    }

    // T-I-11 · PR-11 · integration
    // Given Logs cleared, When freeze, freeze again (no-op), unfreeze, set limit, change limit, remove limit, remove again (no-op), Then exactly five ControlChanged records with ActorId, ActorName "Sam Ellis", CardId, LastFour, Action (freeze, unfreeze, limit_set, limit_changed, limit_removed)
    // Catches: no-ops logged or real changes not logged (AC-39)
    [Fact(Skip = "todo")]
    public void One_control_event_per_real_change()
    {
    }
}
