using System.Globalization;
using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.TestKit.Builders;

/// <summary>Test money from text, e.g. <c>TestMoney.Of("50.00")</c>. Implemented in PR-01.</summary>
public static class TestMoney
{
    /// <summary>Parses invariant decimal text into Money.</summary>
    /// <param name="amount">e.g. <c>"20.01"</c>.</param>
    /// <returns>The money value.</returns>
    /// <exception cref="ArgumentException">Not a valid Money (negative or more than 2 dp).</exception>
    public static Money Of(string amount) =>
        decimal.TryParse(amount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
        && Money.TryFromDecimal(value, out var money)
            ? money
            : throw new ArgumentException($"Not a valid Money: '{amount}'.", nameof(amount));
}

/// <summary>
/// Builds <see cref="StoreSeedCard"/>s with valid defaults (a fresh id, "Test card", "Test Holder", a unique
/// fictional number <c>99990002xxxxxxxx</c>, its last four, lookup hash computed like <see cref="Fakes.FakeCardNumberLookup"/> (on a private instance, so the shared instance's call count is not disturbed), Active).
/// Implemented in PR-01.
/// </summary>
public sealed class CardBuilder
{
    private static int counter;

    private CardId id;
    private string name = "Test card";
    private string cardholder = "Test Holder";
    private string cardNumber;
    private CardStatus status = CardStatus.Active;

    /// <summary>Creates a builder with valid, unique defaults.</summary>
    public CardBuilder()
    {
        var n = Interlocked.Increment(ref counter);
        id = new CardId(new Guid(n, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB, 0xCB));
        cardNumber = "99990002" + n.ToString("D8", CultureInfo.InvariantCulture);
    }

    /// <summary>Sets the id.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithId(CardId id)
    {
        this.id = id;
        return this;
    }

    /// <summary>Sets the card name.</summary>
    /// <param name="name">Name.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    /// <summary>Sets the cardholder.</summary>
    /// <param name="name">Cardholder.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithCardholder(string name)
    {
        cardholder = name;
        return this;
    }

    /// <summary>Sets the card number; last four and lookup hash derive from it.</summary>
    /// <param name="digits">16 digits.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithCardNumber(string digits)
    {
        cardNumber = digits;
        return this;
    }

    /// <summary>Makes the card frozen.</summary>
    /// <returns>This builder.</returns>
    public CardBuilder Frozen()
    {
        status = CardStatus.Frozen;
        return this;
    }

    /// <summary>Builds a seeded card from one of <see cref="Seed.Cards"/> (same name, cardholder, number).</summary>
    /// <param name="card">Seed card.</param>
    /// <returns>A builder preset from the seed card.</returns>
    public static CardBuilder From(SeedCard card) =>
        new CardBuilder().WithName(card.Name).WithCardholder(card.CardholderName).WithCardNumber(card.CardNumber);

    /// <summary>Builds the card for a store seed.</summary>
    /// <returns>The card.</returns>
    public StoreSeedCard Build() =>
        new(id, name, cardholder, cardNumber[^4..], new FakeCardNumberLookup().Compute(cardNumber), status);

    /// <summary>Builds just the card reference (id + last four), e.g. for <c>CardControls</c> tests.</summary>
    /// <returns>The reference.</returns>
    public CardRef BuildRef() => new(id, cardNumber[^4..]);
}

/// <summary>Builds <see cref="SpendLimit"/>s; default Fuel / Day / £50.00. Implemented in PR-01.</summary>
public sealed class LimitBuilder
{
    private Category category = Category.Fuel;
    private Period period = Period.Day;
    private Money amount = TestMoney.Of("50.00");

    /// <summary>Sets the category.</summary>
    /// <param name="category">Category.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder For(Category category)
    {
        this.category = category;
        return this;
    }

    /// <summary>Sets the period.</summary>
    /// <param name="period">Period.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder Per(Period period)
    {
        this.period = period;
        return this;
    }

    /// <summary>Sets the amount.</summary>
    /// <param name="amount">e.g. <c>"80.00"</c>.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder Of(string amount)
    {
        this.amount = TestMoney.Of(amount);
        return this;
    }

    /// <summary>Builds the limit.</summary>
    /// <returns>The limit.</returns>
    public SpendLimit Build() => new(category, period, amount);

    /// <summary>Builds the limit paired with spend already made in the period.</summary>
    /// <param name="approvedSpend">e.g. <c>"30.00"</c>.</param>
    /// <returns>The limit with spend.</returns>
    public LimitWithSpend WithSpend(string approvedSpend) => new(Build(), TestMoney.Of(approvedSpend));
}

/// <summary>
/// Builds raw requests; defaults are valid: Ops van's number, <c>"10.00"</c>, <c>"fuel"</c>, <c>"Test Fuel Ltd"</c>,
/// <c>"2026-11-04T10:00:00Z"</c>. <c>null</c> arguments mean "field missing". Implemented in PR-01.
/// </summary>
public sealed class AuthorisationRequestBuilder
{
    private string? cardNumber = Seed.OpsVan.CardNumber;
    private string? amount = "10.00";
    private string? category = "fuel";
    private string? merchant = "Test Fuel Ltd";
    private string? timestamp = "2026-11-04T10:00:00Z";

    /// <summary>Sets the card number.</summary>
    /// <param name="cardNumber">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithCardNumber(string? cardNumber)
    {
        this.cardNumber = cardNumber;
        return this;
    }

    /// <summary>Sets the amount.</summary>
    /// <param name="amount">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithAmount(string? amount)
    {
        this.amount = amount;
        return this;
    }

    /// <summary>Sets the category wire name.</summary>
    /// <param name="category">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithCategory(string? category)
    {
        this.category = category;
        return this;
    }

    /// <summary>Sets the merchant.</summary>
    /// <param name="merchant">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithMerchant(string? merchant)
    {
        this.merchant = merchant;
        return this;
    }

    /// <summary>Sets the timestamp text.</summary>
    /// <param name="timestamp">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithTimestamp(string? timestamp)
    {
        this.timestamp = timestamp;
        return this;
    }

    /// <summary>Sets the timestamp from an instant (ISO-8601 round-trip format).</summary>
    /// <param name="timestamp">Instant.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder At(DateTimeOffset timestamp)
    {
        this.timestamp = timestamp.ToString("O", CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>Builds the processor request.</summary>
    /// <returns>The request.</returns>
    public AuthorisationRequest Build() => new(cardNumber, amount, category, merchant, timestamp);

    /// <summary>Builds the simulate-path purchase with the same amount, category, merchant and timestamp.</summary>
    /// <returns>The purchase.</returns>
    public SimulatedPurchase BuildPurchase() => new(amount, category, merchant, timestamp);
}

/// <summary>
/// Builds <see cref="NewTransaction"/>s; default approved Fuel £10.00 "Test Fuel Ltd" at 2026-11-04T10:00:00Z, decided at
/// the same instant. Implemented in PR-01.
/// </summary>
public sealed class TransactionBuilder
{
    private Money amount = TestMoney.Of("10.00");
    private Category category = Category.Fuel;
    private DateTimeOffset occurredAt = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
    private DateTimeOffset? decidedAtUtc;
    private Outcome outcome = Outcome.Approved;
    private Reason reason = Reason.Approved;

    /// <summary>Sets the amount.</summary>
    /// <param name="amount">e.g. <c>"30.00"</c>.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder Amount(string amount)
    {
        this.amount = TestMoney.Of(amount);
        return this;
    }

    /// <summary>Sets the category.</summary>
    /// <param name="category">Category.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder In(Category category)
    {
        this.category = category;
        return this;
    }

    /// <summary>Sets <c>OccurredAt</c> (and <c>DecidedAtUtc</c> unless set separately).</summary>
    /// <param name="occurredAt">Instant.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder At(DateTimeOffset occurredAt)
    {
        this.occurredAt = occurredAt;
        return this;
    }

    /// <summary>Sets <c>DecidedAtUtc</c>.</summary>
    /// <param name="decidedAtUtc">Instant.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder DecidedAt(DateTimeOffset decidedAtUtc)
    {
        this.decidedAtUtc = decidedAtUtc;
        return this;
    }

    /// <summary>Approved / <c>Approved</c> (default).</summary>
    /// <returns>This builder.</returns>
    public TransactionBuilder Approved()
    {
        outcome = Outcome.Approved;
        reason = Reason.Approved;
        return this;
    }

    /// <summary>Declined with a reason.</summary>
    /// <param name="reason"><c>CardFrozen</c> or <c>LimitExceeded</c>.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder Declined(Reason reason)
    {
        outcome = Outcome.Declined;
        this.reason = reason;
        return this;
    }

    /// <summary>Builds the transaction.</summary>
    /// <returns>The transaction.</returns>
    public NewTransaction Build() =>
        new(amount, category, "Test Fuel Ltd", occurredAt, decidedAtUtc ?? occurredAt, outcome, reason);
}

/// <summary>Builds <see cref="Actor"/>s; default is the seeded admin. Implemented in PR-01.</summary>
public sealed class ActorBuilder
{
    private string name = Seed.AdminName;

    /// <summary>The seeded admin (<see cref="Seed.AdminId"/>, "Sam Ellis").</summary>
    public static Actor Sam => new(Seed.AdminId, Seed.AdminName);

    /// <summary>Sets the name.</summary>
    /// <param name="name">Display name.</param>
    /// <returns>This builder.</returns>
    public ActorBuilder Named(string name)
    {
        this.name = name;
        return this;
    }

    /// <summary>Builds the actor.</summary>
    /// <returns>The actor.</returns>
    public Actor Build() => new(Seed.AdminId, name);
}
