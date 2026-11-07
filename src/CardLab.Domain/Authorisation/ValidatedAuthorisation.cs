using CardLab.Domain.Limits;

namespace CardLab.Domain.Authorisation;

/// <summary>A processor request that passed <see cref="TransactionRules.Validate"/>.</summary>
/// <param name="CardNumberDigits">Exactly 16 ASCII digits.</param>
/// <param name="Amount">£0.01..£1,000,000.00, at most 2 dp.</param>
/// <param name="Category">Parsed category.</param>
/// <param name="Merchant">Trimmed, 1..200 characters.</param>
/// <param name="OccurredAt">Request timestamp truncated to whole seconds, offset preserved (stored as <c>datetimeoffset(0)</c>).</param>
public sealed record ValidatedAuthorisation(string CardNumberDigits, Money Amount, Category Category, string Merchant, DateTimeOffset OccurredAt)
{
    /// <summary>The purchase part, without the card number.</summary>
    public ValidatedPurchase Purchase => new(Amount, Category, Merchant, OccurredAt);

    /// <summary>Text form with the card number masked to its last four digits (NFR-1).</summary>
    /// <returns>Never contains 16 consecutive digits.</returns>
    public override string ToString() =>
        $"ValidatedAuthorisation {{ CardNumber = {TransactionRules.MaskCardNumber(CardNumberDigits)}, Amount = {Amount}, Category = {Category}, Merchant = {Merchant}, OccurredAt = {OccurredAt:O} }}";
}

/// <summary>A validated purchase (amount, category, merchant, timestamp) for a card resolved by number or id.</summary>
/// <param name="Amount">£0.01..£1,000,000.00, at most 2 dp.</param>
/// <param name="Category">Parsed category.</param>
/// <param name="Merchant">Trimmed, 1..200 characters.</param>
/// <param name="OccurredAt">Timestamp truncated to whole seconds.</param>
public sealed record ValidatedPurchase(Money Amount, Category Category, string Merchant, DateTimeOffset OccurredAt);

/// <summary>One reason a request is malformed; returned in the decision body's <c>problems</c> list (DEC-14).</summary>
/// <param name="Field">Wire field name: <c>cardNumber</c>, <c>amount</c>, <c>category</c>, <c>merchant</c> or <c>timestamp</c>.</param>
/// <param name="Message">Short English (UK) message for an inline field error. Never echoes the card number.</param>
public sealed record ValidationProblem(string Field, string Message);
