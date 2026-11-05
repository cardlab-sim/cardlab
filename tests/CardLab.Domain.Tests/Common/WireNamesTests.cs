using System.Globalization;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;

namespace CardLab.Domain.Tests.Common;

public sealed class WireNamesTests
{
    // T-U-32 · PR-01 · unit
    // Given every Category, Period, CardStatus, Outcome, Reason and AuditAction, When ToWire then TryParse (category/period), Then the design §7 names (e.g. meals_and_entertainment, limit_changed) round-trip; "Fuel", " fuel", "petrol", null fail
    // Catches: wire-name drift between API, SPA and tests
    [Fact]
    public void Wire_names_match_the_api_contract_and_round_trip()
    {
        var categories = new Dictionary<Category, string>
        {
            [Category.Travel] = "travel",
            [Category.Fuel] = "fuel",
            [Category.MealsAndEntertainment] = "meals_and_entertainment",
            [Category.SoftwareAndSubscriptions] = "software_and_subscriptions",
            [Category.OfficeSupplies] = "office_supplies",
            [Category.Advertising] = "advertising",
            [Category.ProfessionalServices] = "professional_services",
            [Category.Other] = "other",
        };
        Assert.Equal(Enum.GetValues<Category>().Length, categories.Count);
        foreach (var (category, wire) in categories)
        {
            Assert.Equal(wire, WireNames.ToWire(category));
            Assert.True(WireNames.TryParseCategory(wire, out var parsed));
            Assert.Equal(category, parsed);
        }

        var periods = new Dictionary<Period, string> { [Period.Day] = "day", [Period.Week] = "week", [Period.Month] = "month" };
        Assert.Equal(Enum.GetValues<Period>().Length, periods.Count);
        foreach (var (period, wire) in periods)
        {
            Assert.Equal(wire, WireNames.ToWire(period));
            Assert.True(WireNames.TryParsePeriod(wire, out var parsed));
            Assert.Equal(period, parsed);
        }

        Assert.Equal("active", WireNames.ToWire(CardStatus.Active));
        Assert.Equal("frozen", WireNames.ToWire(CardStatus.Frozen));
        Assert.Equal("approved", WireNames.ToWire(Outcome.Approved));
        Assert.Equal("declined", WireNames.ToWire(Outcome.Declined));
        Assert.Equal("approved", WireNames.ToWire(Reason.Approved));
        Assert.Equal("card_frozen", WireNames.ToWire(Reason.CardFrozen));
        Assert.Equal("limit_exceeded", WireNames.ToWire(Reason.LimitExceeded));
        Assert.Equal("card_not_found", WireNames.ToWire(Reason.CardNotFound));
        Assert.Equal("invalid_request", WireNames.ToWire(Reason.InvalidRequest));
        Assert.Equal("freeze", WireNames.ToWire(AuditAction.Freeze));
        Assert.Equal("unfreeze", WireNames.ToWire(AuditAction.Unfreeze));
        Assert.Equal("limit_set", WireNames.ToWire(AuditAction.LimitSet));
        Assert.Equal("limit_changed", WireNames.ToWire(AuditAction.LimitChanged));
        Assert.Equal("limit_removed", WireNames.ToWire(AuditAction.LimitRemoved));

        foreach (var bad in new string?[] { "Fuel", " fuel", "fuel ", "petrol", "", null })
        {
            Assert.False(WireNames.TryParseCategory(bad, out _));
        }

        foreach (var bad in new string?[] { "Day", " day", "year", "", null })
        {
            Assert.False(WireNames.TryParsePeriod(bad, out _));
        }
    }

    // T-U-33 · PR-01 · unit
    // Given "50.00", "20", "-1.00", "10.005", "1e3", "1,000.00", " 5", "", null, When TryParseAmount, Then the first four parse (to 50.00, 20, -1.00, 10.005) and the rest fail
    // Catches: culture-dependent or exponent parsing; rejecting "-1.00" before the bounds check can report it (AC-9)
    [Theory]
    [InlineData("50.00", true, "50.00")]
    [InlineData("20", true, "20")]
    [InlineData("-1.00", true, "-1.00")]
    [InlineData("10.005", true, "10.005")]
    [InlineData("10.000000000000000000000000000001", false, null)]
    [InlineData("10.500", true, "10.5")]
    [InlineData("1e3", false, null)]
    [InlineData("1,000.00", false, null)]
    [InlineData(" 5", false, null)]
    [InlineData("5 ", false, null)]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void Amount_text_parsing_is_plain_invariant_decimal(string? text, bool expectedOk, string? expectedValue)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var ok = WireNames.TryParseAmount(text, out var value);

            Assert.Equal(expectedOk, ok);
            if (expectedOk)
            {
                Assert.Equal(decimal.Parse(expectedValue!, CultureInfo.InvariantCulture), value);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
