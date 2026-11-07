using System.Globalization;
using System.Text.RegularExpressions;
using CardLab.Domain.Common;

namespace CardLab.Domain.Authorisation;

/// <summary>
/// Validation of incoming transaction requests (R-7, R-11, AC-20, design §9). Pure; no I/O.
/// </summary>
/// <remarks>
/// Rules, each producing one <see cref="ValidationProblem"/> (all problems are reported, not just the first):
/// <list type="bullet">
/// <item><c>cardNumber</c> (processor path only): required; exactly 16 ASCII digits, no spaces or separators.</item>
/// <item><c>amount</c>: required; <see cref="WireNames.TryParseAmount"/>; <see cref="Money.TryFromDecimal"/>
/// (at most 2 dp, not negative); <see cref="MoneyBounds.IsValidTransactionAmount"/> (£0.01..£1,000,000.00).</item>
/// <item><c>category</c>: required; <see cref="WireNames.TryParseCategory"/>.</item>
/// <item><c>merchant</c>: required; non-blank after trimming; at most 200 characters; no run of 13 or more digits,
/// optionally separated by single spaces or dashes (card-number-shaped, NFR-1, BUG-005). The message never echoes the text.</item>
/// <item><c>timestamp</c>: required on the processor path, optional on the simulate path; strict ISO-8601
/// (<c>yyyy-MM-ddTHH:mm:ss[.fffffff](Z|±hh:mm)</c>, explicit offset; any other text is a problem) within
/// [2000-01-01T00:00Z, 2100-01-01T00:00Z); truncated to whole seconds, offset preserved.</item>
/// </list>
/// Messages never echo the submitted card number. Implemented in PR-04. High-risk path.
/// </remarks>
public static partial class TransactionRules
{
    private const int MaxMerchantLength = 200;

    // Supported timestamp window (BUG-004): keeps PeriodCalendar total over everything validation accepts.
    private static readonly DateTimeOffset MinTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MaxTimestampExclusive = new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Validates a processor request.</summary>
    /// <param name="raw">The request as received.</param>
    /// <returns>The validated request, or every problem found (non-empty). A failure maps to reason <c>invalid_request</c>.</returns>
    public static Result<ValidatedAuthorisation, IReadOnlyList<ValidationProblem>> Validate(AuthorisationRequest raw)
    {
        var problems = new List<ValidationProblem>();

        var cardNumber = raw.CardNumber;
        if (!IsSixteenAsciiDigits(cardNumber))
        {
            problems.Add(new ValidationProblem("cardNumber", "Card number must be exactly sixteen digits with no spaces."));
        }

        var purchase = ValidateFields(raw.Amount, raw.Category, raw.Merchant, raw.Timestamp, null, problems);

        return problems.Count == 0
            ? Result<ValidatedAuthorisation, IReadOnlyList<ValidationProblem>>.Ok(
                new ValidatedAuthorisation(cardNumber!, purchase!.Amount, purchase.Category, purchase.Merchant, purchase.OccurredAt))
            : Result<ValidatedAuthorisation, IReadOnlyList<ValidationProblem>>.Fail(problems);
    }

    /// <summary>Masks a card number to its last four digits for text output (NFR-1, design §10).</summary>
    /// <param name="cardNumber">The submitted or validated card number; may be null.</param>
    /// <returns><c>************</c> plus the last four characters; <c>&lt;missing&gt;</c> when null or empty; <c>&lt;invalid&gt;</c> when shorter than four characters.</returns>
    internal static string MaskCardNumber(string? cardNumber) =>
        string.IsNullOrEmpty(cardNumber) ? "<missing>"
        : cardNumber.Length < 4 ? "<invalid>"
        : "************" + cardNumber[^4..];

    private static bool IsSixteenAsciiDigits(string? text)
    {
        if (text is null || text.Length != 16)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (ch is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static ValidatedPurchase? ValidateFields(
        string? amountText,
        string? categoryText,
        string? merchantText,
        string? timestampText,
        DateTimeOffset? defaultOccurredAt,
        List<ValidationProblem> problems)
    {
        var amount = Money.Zero;
        var amountOk = WireNames.TryParseAmount(amountText, out var amountValue)
            && Money.TryFromDecimal(amountValue, out amount)
            && MoneyBounds.IsValidTransactionAmount(amount);
        if (!amountOk)
        {
            problems.Add(new ValidationProblem("amount", "Amount must be a number between 0.01 and 1000000.00 with at most two decimal places."));
        }

        var categoryOk = WireNames.TryParseCategory(categoryText, out var category);
        if (!categoryOk)
        {
            problems.Add(new ValidationProblem("category", "Category is missing or not recognised."));
        }

        var merchant = merchantText?.Trim() ?? string.Empty;
        var merchantOk = merchant.Length is >= 1 and <= MaxMerchantLength && !CardNumberLikeRun().IsMatch(merchant);
        if (!merchantOk)
        {
            problems.Add(new ValidationProblem("merchant", "Merchant must be between 1 and 200 characters and must not contain a card number."));
        }

        DateTimeOffset occurredAt = default;
        bool timestampOk;
        if (string.IsNullOrEmpty(timestampText) && defaultOccurredAt is not null)
        {
            occurredAt = TruncateToSeconds(defaultOccurredAt.Value);
            timestampOk = true;
        }
        else if (!string.IsNullOrEmpty(timestampText)
            && IsoInstant().IsMatch(timestampText)
            && DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            && parsed >= MinTimestamp
            && parsed < MaxTimestampExclusive)
        {
            occurredAt = TruncateToSeconds(parsed);
            timestampOk = true;
        }
        else
        {
            timestampOk = false;
            problems.Add(new ValidationProblem("timestamp", "Timestamp must be an ISO 8601 date and time with an explicit offset, for example 2026-11-04T10:00:00Z."));
        }

        return amountOk && categoryOk && merchantOk && timestampOk
            ? new ValidatedPurchase(amount, category, merchant, occurredAt)
            : null;
    }

    // 13+ digits, each pair optionally separated by one space or dash (a PAN is 13..19 digits).
    [GeneratedRegex(@"[0-9](?:[ -]?[0-9]){12,}")]
    private static partial Regex CardNumberLikeRun();

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));

    // Strict ISO 8601 extended shape with an explicit offset; TryParse then checks calendar validity.
    // [0-9], not \d: \d also matches non-ASCII digits.
    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})\z")]
    private static partial Regex IsoInstant();

    /// <summary>Validates a simulated purchase (card already identified by id).</summary>
    /// <param name="raw">The purchase as received.</param>
    /// <param name="defaultOccurredAt">Used when <c>raw.Timestamp</c> is null or empty (the service passes <c>IClock.UtcNow</c>); truncated to whole seconds like a supplied timestamp.</param>
    /// <returns>The validated purchase, or every problem found (non-empty).</returns>
    public static Result<ValidatedPurchase, IReadOnlyList<ValidationProblem>> ValidatePurchase(SimulatedPurchase raw, DateTimeOffset defaultOccurredAt)
    {
        var problems = new List<ValidationProblem>();
        var purchase = ValidateFields(raw.Amount, raw.Category, raw.Merchant, raw.Timestamp, defaultOccurredAt, problems);

        return problems.Count == 0
            ? Result<ValidatedPurchase, IReadOnlyList<ValidationProblem>>.Ok(purchase!)
            : Result<ValidatedPurchase, IReadOnlyList<ValidationProblem>>.Fail(problems);
    }
}
