using CardLab.Domain.Authorisation;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Logging;

[Collection("sql")]
public sealed class EventTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EventTests(ApiFactory factory) => _factory = factory;

    private static Dictionary<string, object?> Fields(CapturedRecord record, params string[] names)
    {
        foreach (var name in names)
        {
            Assert.True(record.Attributes.ContainsKey(name), $"'{record.EventName}' record lacks field '{name}'.");
        }

        return names.ToDictionary(n => n, n => record.Attributes[n]);
    }

    private static void AssertAuthorisation(CapturedRecord record, string cardId, string lastFour, string category, string amount, string outcome, string reason)
    {
        var fields = Fields(record, "CardId", "LastFour", "Category", "Amount", "Outcome", "Reason", "DurationMs");
        Assert.Equal(cardId, fields["CardId"]);
        Assert.Equal(lastFour, fields["LastFour"]);
        Assert.Equal(category, fields["Category"]);
        Assert.Equal(amount, fields["Amount"]);
        Assert.Equal(outcome, fields["Outcome"]);
        Assert.Equal(reason, fields["Reason"]);
        Assert.True(Convert.ToInt64(fields["DurationMs"], System.Globalization.CultureInfo.InvariantCulture) >= 0);
    }

    private static AuthorisationRequest FuelRequest(string cardNumber, string amount) =>
        new AuthorisationRequestBuilder().WithCardNumber(cardNumber).WithAmount(amount).WithCategory("fuel").WithTimestamp("2026-11-04T10:00:00Z").Build();

    // T-I-11 · PR-11 · integration
    // Given Logs cleared, When one processor authorisation, one simulate purchase, one unknown-card and one invalid request are made, Then exactly four AuthorisationDecided records with CardId, LastFour, Category, Amount, Outcome, Reason, DurationMs (CardId/LastFour "-" for the last two), and no record contains a 16-digit run
    // Catches: endpoint-level double logging, missing fields, simulate path not logging (AC-39, NFR-7)
    [Fact]
    public async Task One_authorisation_event_per_decision()
    {
        var client = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value.ToString();
        _factory.Logs.Clear();

        await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan.CardNumber, "5.00"));
        await client.SimulatePurchaseAsync(Guid.Parse(id), new SimulatedPurchase("6.00", "travel", "Corner shop", "2026-11-04T11:00:00Z"));
        await client.AuthoriseAsync(FuelRequest(TestSeed.UnknownCardNumber, "7.00"));
        await client.AuthoriseAsync(FuelRequest(TestSeed.OpsVan.CardNumber, "0.00"));

        var events = _factory.Logs.All("AuthorisationDecided");
        Assert.Equal(4, events.Count);
        AssertAuthorisation(events[0], id, "0001", "fuel", "5.00", "approved", "approved");
        AssertAuthorisation(events[1], id, "0001", "travel", "6.00", "approved", "approved");
        AssertAuthorisation(events[2], "-", "-", "fuel", "7.00", "declined", "card_not_found");
        AssertAuthorisation(events[3], "-", "-", "fuel", "-", "declined", "invalid_request");
        _factory.Logs.AssertNoPan();
    }

    // T-I-11 · PR-11 · integration
    // Given Logs cleared, When freeze, freeze again (no-op), unfreeze, set limit, change limit, remove limit, remove again (no-op), Then exactly five ControlChanged records with ActorId, ActorName "Sam Ellis", CardId, LastFour, Action (freeze, unfreeze, limit_set, limit_changed, limit_removed)
    // Catches: no-ops logged or real changes not logged (AC-39)
    [Fact]
    public async Task One_control_event_per_real_change()
    {
        var client = _factory.CreateApiClient();
        var id = (await _factory.Database.CardIdAsync(TestSeed.FieldSales.Name)).Value;
        _factory.Logs.Clear();

        await client.FreezeAsync(id);
        await client.FreezeAsync(id);
        await client.UnfreezeAsync(id);
        await client.SetLimitAsync(id, "fuel", "day", "50.00");
        await client.SetLimitAsync(id, "fuel", "day", "30.00");
        await client.RemoveLimitAsync(id, "fuel", "day");
        await client.RemoveLimitAsync(id, "fuel", "day");

        var events = _factory.Logs.All("ControlChanged");
        Assert.Equal(["freeze", "unfreeze", "limit_set", "limit_changed", "limit_removed"], events.Select(e => e.Attributes["Action"]));
        foreach (var e in events)
        {
            var fields = Fields(e, "ActorId", "ActorName", "CardId", "LastFour", "Action");
            Assert.Equal(TestSeed.AdminId.ToString(), fields["ActorId"]?.ToString(), ignoreCase: true);
            Assert.Equal("Sam Ellis", fields["ActorName"]);
            Assert.Equal(id.ToString(), fields["CardId"]);
            Assert.Equal("0002", fields["LastFour"]);
        }

        _factory.Logs.AssertNoPan();
    }
}
