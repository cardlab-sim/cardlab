using CardLab.Domain.Authorisation;
using CardLab.Domain.Limits;
using CardLab.TestKit;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Authorisation;

public sealed class TransactionRulesTests
{
    private static AuthorisationRequestBuilder Valid() => new AuthorisationRequestBuilder().WithAmount("20.00");

    // T-U-17 · PR-04 · unit (theory)
    // Given a valid request with one field changed: amount missing / "0.00" / "10.005" / "abc" / "1000000.01"; category "petrol" / missing; timestamp "yesterday" / "2026-11-04T10:00:00" (no offset) / missing; merchant blank, When Validate, Then failure with exactly one problem naming that field
    // Catches: a malformed case slipping through as a stored transaction (AC-20)
    [Theory]
    [InlineData("amount", null)]
    [InlineData("amount", "")]
    [InlineData("amount", "0.00")]
    [InlineData("amount", "0")]
    [InlineData("amount", "10.005")]
    [InlineData("amount", "abc")]
    [InlineData("amount", "-1.00")]
    [InlineData("amount", "1000000.01")]
    [InlineData("category", "petrol")]
    [InlineData("category", "Fuel")]
    [InlineData("category", null)]
    [InlineData("timestamp", "yesterday")]
    [InlineData("timestamp", "2026-11-04T10:00:00")]
    [InlineData("timestamp", null)]
    [InlineData("timestamp", "")]
    [InlineData("timestamp", "9999-12-31T12:00:00Z")]
    [InlineData("timestamp", "1999-12-31T23:59:59Z")]
    [InlineData("timestamp", "2100-01-01T00:00:00Z")]
    [InlineData("timestamp", "2099-12-31T23:59:59-01:00")]
    [InlineData("timestamp", "04/11/2026 10:00:00 +00:00")]
    [InlineData("timestamp", "2026-11-04Z")]
    [InlineData("timestamp", "2026-11-04")]
    [InlineData("timestamp", "Wednesday, 04 November 2026 10:00:00 +00:00")]
    [InlineData("timestamp", "2026-11-04 10:00:00Z")]
    [InlineData("timestamp", "2026-11-04T10:00Z")]
    [InlineData("timestamp", "2026-11-04T10:00:00Z\n")]
    [InlineData("timestamp", "2026-02-30T10:00:00Z")]
    [InlineData("merchant", "")]
    [InlineData("merchant", "   ")]
    [InlineData("merchant", null)]
    public void Each_malformed_field_is_reported(string field, string? value)
    {
        var builder = Valid();
        _ = field switch
        {
            "amount" => builder.WithAmount(value),
            "category" => builder.WithCategory(value),
            "timestamp" => builder.WithTimestamp(value),
            "merchant" => builder.WithMerchant(value),
            _ => throw new ArgumentException(field),
        };

        var result = TransactionRules.Validate(builder.Build());

        Assert.False(result.IsOk);
        var problem = Assert.Single(result.Error);
        Assert.Equal(field, problem.Field);
        Assert.False(string.IsNullOrWhiteSpace(problem.Message));
    }

    // Added to T-U-17: the accepted edges of the amount, merchant and timestamp rules (boundary mutations).
    [Theory]
    [InlineData("0.01")]
    [InlineData("1000000.00")]
    [InlineData("10.5")]
    public void Amounts_inside_the_bounds_are_accepted(string amount)
    {
        var result = TransactionRules.Validate(Valid().WithAmount(amount).Build());

        Assert.True(result.IsOk);
    }

    // Added to T-U-17: merchant length bound is 200 after trimming.
    [Fact]
    public void Merchant_is_limited_to_200_characters_after_trimming()
    {
        var ok = TransactionRules.Validate(Valid().WithMerchant(" " + new string('m', 200) + " ").Build());
        var tooLong = TransactionRules.Validate(Valid().WithMerchant(new string('m', 201)).Build());

        Assert.True(ok.IsOk);
        Assert.Equal(200, ok.Value.Merchant.Length);
        Assert.False(tooLong.IsOk);
        Assert.Equal("merchant", Assert.Single(tooLong.Error).Field);
    }

    // BUG-005 (NFR-1): a card-number-like run of 13+ digits (optionally spaced or dashed) in the merchant is rejected.
    [Theory]
    [InlineData("9999000100000001")]
    [InlineData("9999 0001 0000 0001")]
    [InlineData("9999-0001-0000-0001")]
    [InlineData("Shell 9999000100000001 Ltd")]
    [InlineData("1234567890123")]
    [InlineData("1234 5678 9012 3")]
    public void Merchant_containing_a_card_number_like_run_is_rejected_on_both_paths(string merchant)
    {
        var processor = TransactionRules.Validate(Valid().WithMerchant(merchant).Build());
        var purchase = TransactionRules.ValidatePurchase(Valid().WithMerchant(merchant).BuildPurchase(), DateTimeOffset.UnixEpoch);

        Assert.False(processor.IsOk);
        var processorProblem = Assert.Single(processor.Error);
        Assert.Equal("merchant", processorProblem.Field);
        Assert.DoesNotContain("9999", processorProblem.Message);
        Assert.False(purchase.IsOk);
        Assert.Equal("merchant", Assert.Single(purchase.Error).Field);
    }

    // BUG-005: ordinary digits in a merchant name stay valid (12 digits is below the 13-digit threshold).
    [Theory]
    [InlineData("Northway Fuel 24")]
    [InlineData("Unit 12345")]
    [InlineData("123456789012")]
    [InlineData("Depot 1234 Store 5678 Bay 9012 Gate 3")]
    public void Merchant_with_ordinary_digits_is_accepted(string merchant)
    {
        Assert.True(TransactionRules.Validate(Valid().WithMerchant(merchant).Build()).IsOk);
        Assert.True(TransactionRules.ValidatePurchase(Valid().WithMerchant(merchant).BuildPurchase(), DateTimeOffset.UnixEpoch).IsOk);
    }

    // Added to T-U-17: every problem is reported, one per field, in one result.
    [Fact]
    public void All_problems_are_reported_together()
    {
        var raw = new AuthorisationRequest("123", "abc", "petrol", " ", "yesterday");

        var result = TransactionRules.Validate(raw);

        Assert.False(result.IsOk);
        Assert.Equal(
            ["amount", "cardNumber", "category", "merchant", "timestamp"],
            result.Error.Select(p => p.Field).Order(StringComparer.Ordinal).ToArray());
    }

    // T-U-28 · PR-04 · unit
    // Given card numbers of 15 and 17 digits, with spaces, with letters, and missing, When Validate, Then a cardNumber problem whose message contains no digits of the submitted number
    // Catches: non-16-digit numbers reaching the lookup; PAN echoed in an error (NFR-1)
    [Theory]
    [InlineData("999900010000000")]
    [InlineData("99990001000000012")]
    [InlineData("9999 0001 0000 0001")]
    [InlineData("9999000100000O01")]
    [InlineData("٩٩٩٩٠٠٠١٠٠٠٠٠٠٠١")]
    [InlineData("")]
    [InlineData(null)]
    public void Card_number_must_be_exactly_16_digits_and_is_never_echoed(string? cardNumber)
    {
        var result = TransactionRules.Validate(Valid().WithCardNumber(cardNumber).Build());

        Assert.False(result.IsOk);
        var problem = Assert.Single(result.Error);
        Assert.Equal("cardNumber", problem.Field);
        Assert.DoesNotContain(problem.Message, ch => char.IsDigit(ch));
    }

    // T-U-29 · PR-04 · unit
    // Given a valid request with timestamp "2026-10-25T00:30:15.789+01:00", When Validate, Then OccurredAt is 2026-10-25T00:30:15+01:00 (seconds kept, fraction dropped, offset kept), merchant trimmed, amount 20.00
    // Catches: timestamps shifted to the server clock or zone (R-13), precision drift vs datetimeoffset(0)
    [Fact]
    public void Valid_request_is_normalised()
    {
        var raw = Valid().WithTimestamp("2026-10-25T00:30:15.789+01:00").WithMerchant("  Shell Garage  ").Build();

        var result = TransactionRules.Validate(raw);

        Assert.True(result.IsOk);
        var valid = result.Value;
        var expected = new DateTimeOffset(2026, 10, 25, 0, 30, 15, TimeSpan.FromHours(1));
        Assert.Equal(expected, valid.OccurredAt);
        Assert.Equal(expected.Ticks, valid.OccurredAt.Ticks);
        Assert.Equal(TimeSpan.FromHours(1), valid.OccurredAt.Offset);
        Assert.Equal("Shell Garage", valid.Merchant);
        Assert.Equal(TestMoney.Of("20.00"), valid.Amount);
        Assert.Equal(Category.Fuel, valid.Category);
        Assert.Equal(Seed.OpsVan.CardNumber, valid.CardNumberDigits);
    }

    // Added to T-U-17 (BUG-004): the supported window is [2000-01-01T00:00Z, 2100-01-01T00:00Z); its edges are accepted.
    [Theory]
    [InlineData("2099-12-31T23:59:59Z")]
    [InlineData("2000-01-01T00:00:00Z")]
    [InlineData("2099-12-31T23:59:59.9999999Z")]
    [InlineData("2000-01-01T01:00:00+01:00")]
    public void Timestamps_inside_the_supported_window_are_accepted(string timestamp)
    {
        Assert.True(TransactionRules.Validate(Valid().WithTimestamp(timestamp).Build()).IsOk);
        Assert.True(TransactionRules.ValidatePurchase(Valid().WithTimestamp(timestamp).BuildPurchase(), DateTimeOffset.UnixEpoch).IsOk);
    }

    // Added to T-U-30 (BUG-004): the same strict rule applies to a supplied simulate timestamp.
    [Theory]
    [InlineData("9999-12-31T12:00:00Z")]
    [InlineData("1999-12-31T23:59:59Z")]
    [InlineData("04/11/2026 10:00:00 +00:00")]
    [InlineData("2026-11-04")]
    [InlineData("Wednesday, 04 November 2026 10:00:00 +00:00")]
    public void Purchase_rejects_non_iso_and_out_of_window_timestamps(string timestamp)
    {
        var result = TransactionRules.ValidatePurchase(Valid().WithTimestamp(timestamp).BuildPurchase(), DateTimeOffset.UnixEpoch);

        Assert.False(result.IsOk);
        Assert.Equal("timestamp", Assert.Single(result.Error).Field);
    }

    // Added to T-U-29: a Z suffix is an explicit offset and is preserved as UTC.
    [Fact]
    public void Utc_timestamp_keeps_a_zero_offset()
    {
        var result = TransactionRules.Validate(Valid().WithTimestamp("2026-10-24T23:30:00Z").Build());

        Assert.True(result.IsOk);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.Zero), result.Value.OccurredAt);
        Assert.Equal(TimeSpan.Zero, result.Value.OccurredAt.Offset);
    }

    // T-U-30 · PR-04 · unit
    // Given a SimulatedPurchase without timestamp and one with an invalid amount, When ValidatePurchase(raw, default), Then the first uses the default timestamp; the second reports amount; no cardNumber rule applies
    // Catches: simulate path demanding a card number or ignoring the default timestamp (DEC-14)
    [Fact]
    public void Purchase_validation_defaults_timestamp_and_skips_card_number()
    {
        var fallback = new DateTimeOffset(2026, 11, 5, 9, 15, 0, TimeSpan.Zero).AddTicks(7_890_000);
        var withoutTimestamp = Valid().WithTimestamp(null).BuildPurchase();
        var badAmount = Valid().WithAmount("abc").BuildPurchase();

        var ok = TransactionRules.ValidatePurchase(withoutTimestamp, fallback);
        var bad = TransactionRules.ValidatePurchase(badAmount, fallback);

        Assert.True(ok.IsOk);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 9, 15, 0, TimeSpan.Zero).Ticks, ok.Value.OccurredAt.Ticks);
        Assert.Equal(TestMoney.Of("20.00"), ok.Value.Amount);
        Assert.False(bad.IsOk);
        Assert.Equal("amount", Assert.Single(bad.Error).Field);
    }

    // Added to T-U-30: an explicit timestamp wins over the default, and a malformed one is still a problem.
    [Fact]
    public void Purchase_validation_uses_a_supplied_timestamp_and_rejects_a_malformed_one()
    {
        var fallback = new DateTimeOffset(2026, 11, 5, 9, 15, 0, TimeSpan.Zero);

        var supplied = TransactionRules.ValidatePurchase(Valid().WithTimestamp("2026-10-25T00:30:15.789+01:00").BuildPurchase(), fallback);
        var malformed = TransactionRules.ValidatePurchase(Valid().WithTimestamp("2026-11-04T10:00:00").BuildPurchase(), fallback);

        Assert.True(supplied.IsOk);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 15, TimeSpan.FromHours(1)).Ticks, supplied.Value.OccurredAt.Ticks);
        Assert.Equal(TimeSpan.FromHours(1), supplied.Value.OccurredAt.Offset);
        Assert.False(malformed.IsOk);
        Assert.Equal("timestamp", Assert.Single(malformed.Error).Field);
    }

    // T-U-31 · PR-04 · unit
    // Given AuthorisationRequest and ValidatedAuthorisation holding 9999000100000001, When ToString, Then the text contains "0001" but no 16-digit run
    // Catches: PAN leaking through record ToString into logs or exception messages (NFR-1, design §10)
    [Fact]
    public void ToString_masks_the_card_number()
    {
        var raw = Valid().WithCardNumber("9999000100000001").Build();
        var validated = TransactionRules.Validate(raw).Value!;

        var requestText = raw.ToString();
        var validatedText = validated.ToString();

        Assert.Contains("************0001", requestText);
        Assert.Contains("************0001", validatedText);
        Assert.Contains("20.00", requestText);
        Assert.Contains("20.00", validatedText);
        Assert.Contains("Test Fuel Ltd", requestText);
        Assert.Contains("Test Fuel Ltd", validatedText);
        PanScanner.AssertNoPan(requestText, "AuthorisationRequest.ToString()");
        PanScanner.AssertNoPan(validatedText, "ValidatedAuthorisation.ToString()");
        Assert.DoesNotContain("99990001", requestText);
        Assert.DoesNotContain("99990001", validatedText);
    }

    // Added to T-U-31: a missing or too-short card number prints a placeholder instead of throwing.
    [Fact]
    public void ToString_marks_a_missing_or_short_card_number()
    {
        var missing = new AuthorisationRequest(null, "1.00", "fuel", "M", "2026-11-04T10:00:00Z").ToString();
        var shortNumber = new AuthorisationRequest("123", "1.00", "fuel", "M", "2026-11-04T10:00:00Z").ToString();

        Assert.Contains("<missing>", missing);
        Assert.Contains("<invalid>", shortNumber);
        Assert.DoesNotContain("123", shortNumber);
    }
}
