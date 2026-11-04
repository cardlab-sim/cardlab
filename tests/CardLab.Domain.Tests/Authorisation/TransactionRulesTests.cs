namespace CardLab.Domain.Tests.Authorisation;

public sealed class TransactionRulesTests
{
    // T-U-17 · PR-04 · unit (theory)
    // Given a valid request with one field changed: amount missing / "0.00" / "10.005" / "abc" / "1000000.01"; category "petrol" / missing; timestamp "yesterday" / "2026-11-04T10:00:00" (no offset) / missing; merchant blank, When Validate, Then failure with exactly one problem naming that field
    // Catches: a malformed case slipping through as a stored transaction (AC-20)
    [Fact(Skip = "todo")]
    public void Each_malformed_field_is_reported()
    {
    }

    // T-U-28 · PR-04 · unit
    // Given card numbers of 15 and 17 digits, with spaces, with letters, and missing, When Validate, Then a cardNumber problem whose message contains no digits of the submitted number
    // Catches: non-16-digit numbers reaching the lookup; PAN echoed in an error (NFR-1)
    [Fact(Skip = "todo")]
    public void Card_number_must_be_exactly_16_digits_and_is_never_echoed()
    {
    }

    // T-U-29 · PR-04 · unit
    // Given a valid request with timestamp "2026-10-25T00:30:15.789+01:00", When Validate, Then OccurredAt is 2026-10-25T00:30:15+01:00 (seconds kept, fraction dropped, offset kept), merchant trimmed, amount 20.00
    // Catches: timestamps shifted to the server clock or zone (R-13), precision drift vs datetimeoffset(0)
    [Fact(Skip = "todo")]
    public void Valid_request_is_normalised()
    {
    }

    // T-U-30 · PR-04 · unit
    // Given a SimulatedPurchase without timestamp and one with an invalid amount, When ValidatePurchase(raw, default), Then the first uses the default timestamp; the second reports amount; no cardNumber rule applies
    // Catches: simulate path demanding a card number or ignoring the default timestamp (DEC-14)
    [Fact(Skip = "todo")]
    public void Purchase_validation_defaults_timestamp_and_skips_card_number()
    {
    }

    // T-U-31 · PR-04 · unit
    // Given AuthorisationRequest and ValidatedAuthorisation holding 9999000100000001, When ToString, Then the text contains "0001" but no 16-digit run
    // Catches: PAN leaking through record ToString into logs or exception messages (NFR-1, design §10)
    [Fact(Skip = "todo")]
    public void ToString_masks_the_card_number()
    {
    }
}
