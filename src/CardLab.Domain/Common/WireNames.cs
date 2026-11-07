using System.Globalization;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;

namespace CardLab.Domain.Common;

/// <summary>
/// The single mapping between domain enums and their wire names (design §7) plus the wire amount
/// syntax, so the processor API, the admin API and validation all accept and emit the same strings.
/// </summary>
/// <remarks>
/// Wire names: categories <c>travel</c>, <c>fuel</c>, <c>meals_and_entertainment</c>,
/// <c>software_and_subscriptions</c>, <c>office_supplies</c>, <c>advertising</c>,
/// <c>professional_services</c>, <c>other</c>; periods <c>day</c>, <c>week</c>, <c>month</c>;
/// status <c>active</c>, <c>frozen</c>; outcomes <c>approved</c>, <c>declined</c>; reasons
/// <c>approved</c>, <c>card_frozen</c>, <c>limit_exceeded</c>, <c>card_not_found</c>,
/// <c>invalid_request</c>; audit actions <c>freeze</c>, <c>unfreeze</c>, <c>limit_set</c>,
/// <c>limit_changed</c>, <c>limit_removed</c>. Parsing is exact (lower case, no trimming).
/// Implemented in PR-01.
/// </remarks>
public static class WireNames
{
    /// <summary>Wire name of a category, e.g. <c>meals_and_entertainment</c>.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The snake_case wire name.</returns>
    public static string ToWire(Category category) => category switch
    {
        Category.Travel => "travel",
        Category.Fuel => "fuel",
        Category.MealsAndEntertainment => "meals_and_entertainment",
        Category.SoftwareAndSubscriptions => "software_and_subscriptions",
        Category.OfficeSupplies => "office_supplies",
        Category.Advertising => "advertising",
        Category.ProfessionalServices => "professional_services",
        Category.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>Wire name of a period: <c>day</c>, <c>week</c> or <c>month</c>.</summary>
    /// <param name="period">The period.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(Period period) => period switch
    {
        Period.Day => "day",
        Period.Week => "week",
        Period.Month => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
    };

    /// <summary>Wire name of a card status: <c>active</c> or <c>frozen</c>.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(CardStatus status) => status switch
    {
        CardStatus.Active => "active",
        CardStatus.Frozen => "frozen",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>Wire name of an outcome: <c>approved</c> or <c>declined</c>.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(Outcome outcome) => outcome switch
    {
        Outcome.Approved => "approved",
        Outcome.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    /// <summary>Wire name of a reason code (R-7), e.g. <c>limit_exceeded</c>.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(Reason reason) => reason switch
    {
        Reason.Approved => "approved",
        Reason.CardFrozen => "card_frozen",
        Reason.LimitExceeded => "limit_exceeded",
        Reason.CardNotFound => "card_not_found",
        Reason.InvalidRequest => "invalid_request",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    /// <summary>Wire name of an audit action, e.g. <c>limit_changed</c>.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The wire name.</returns>
    public static string ToWire(AuditAction action) => action switch
    {
        AuditAction.Freeze => "freeze",
        AuditAction.Unfreeze => "unfreeze",
        AuditAction.LimitSet => "limit_set",
        AuditAction.LimitChanged => "limit_changed",
        AuditAction.LimitRemoved => "limit_removed",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    /// <summary>Parses a category wire name.</summary>
    /// <param name="text">Candidate wire name; null or unknown fails.</param>
    /// <param name="category">The parsed category when the method returns true.</param>
    /// <returns>True when <paramref name="text"/> is exactly one of the eight wire names.</returns>
    public static bool TryParseCategory(string? text, out Category category)
    {
        foreach (var candidate in Enum.GetValues<Category>())
        {
            if (string.Equals(text, ToWire(candidate), StringComparison.Ordinal))
            {
                category = candidate;
                return true;
            }
        }

        category = default;
        return false;
    }

    /// <summary>Parses a period wire name.</summary>
    /// <param name="text">Candidate wire name; null or unknown fails.</param>
    /// <param name="period">The parsed period when the method returns true.</param>
    /// <returns>True when <paramref name="text"/> is exactly <c>day</c>, <c>week</c> or <c>month</c>.</returns>
    public static bool TryParsePeriod(string? text, out Period period)
    {
        foreach (var candidate in Enum.GetValues<Period>())
        {
            if (string.Equals(text, ToWire(candidate), StringComparison.Ordinal))
            {
                period = candidate;
                return true;
            }
        }

        period = default;
        return false;
    }

    /// <summary>
    /// Parses a wire amount such as <c>"50.00"</c>, <c>"20"</c> or <c>"-1.00"</c> (invariant culture,
    /// optional leading minus, optional decimal point; no exponent, no thousands separators, no spaces).
    /// </summary>
    /// <remarks>Does not apply R-11 bounds or the 2-dp rule; <c>Money.TryFromDecimal</c> and
    /// <c>MoneyBounds</c> do that.</remarks>
    /// <param name="text">Candidate amount text.</param>
    /// <param name="value">The parsed decimal when the method returns true.</param>
    /// <returns>True when the text is a plain decimal number. Fails when the text has more digits than a decimal holds.</returns>
    public static bool TryParseAmount(string? text, out decimal value)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        // decimal.TryParse silently rounds text with more digits than a decimal holds; reject that instead.
        var dot = text!.IndexOf('.');
        if (dot >= 0 && text.AsSpan(dot + 1).TrimEnd('0').Length > value.Scale)
        {
            value = default;
            return false;
        }

        return true;
    }
}
