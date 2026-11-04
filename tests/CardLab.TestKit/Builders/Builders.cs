using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.TestKit.Contracts;

namespace CardLab.TestKit.Builders;

/// <summary>Test money from text, e.g. <c>TestMoney.Of("50.00")</c>. Implemented in PR-01.</summary>
public static class TestMoney
{
    /// <summary>Parses invariant decimal text into Money.</summary>
    /// <param name="amount">e.g. <c>"20.01"</c>.</param>
    /// <returns>The money value.</returns>
    /// <exception cref="ArgumentException">Not a valid Money (negative or more than 2 dp).</exception>
    public static Money Of(string amount) => throw new NotImplementedException("PR-01");
}

/// <summary>
/// Builds <see cref="StoreSeedCard"/>s with valid defaults (a fresh id, "Test card", "Test Holder", a unique
/// fictional number <c>99990002xxxxxxxx</c>, its last four, lookup via <see cref="Fakes.FakeCardNumberLookup"/>, Active).
/// Implemented in PR-01.
/// </summary>
public sealed class CardBuilder
{
    /// <summary>Sets the id.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithId(CardId id) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the card name.</summary>
    /// <param name="name">Name.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithName(string name) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the cardholder.</summary>
    /// <param name="name">Cardholder.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithCardholder(string name) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the card number; last four and lookup hash derive from it.</summary>
    /// <param name="digits">16 digits.</param>
    /// <returns>This builder.</returns>
    public CardBuilder WithCardNumber(string digits) => throw new NotImplementedException("PR-01");

    /// <summary>Makes the card frozen.</summary>
    /// <returns>This builder.</returns>
    public CardBuilder Frozen() => throw new NotImplementedException("PR-01");

    /// <summary>Builds a seeded card from one of <see cref="Seed.Cards"/> (same name, cardholder, number).</summary>
    /// <param name="card">Seed card.</param>
    /// <returns>A builder preset from the seed card.</returns>
    public static CardBuilder From(SeedCard card) => throw new NotImplementedException("PR-01");

    /// <summary>Builds the card for a store seed.</summary>
    /// <returns>The card.</returns>
    public StoreSeedCard Build() => throw new NotImplementedException("PR-01");

    /// <summary>Builds just the card reference (id + last four), e.g. for <c>CardControls</c> tests.</summary>
    /// <returns>The reference.</returns>
    public CardRef BuildRef() => throw new NotImplementedException("PR-01");
}

/// <summary>Builds <see cref="SpendLimit"/>s; default Fuel / Day / £50.00. Implemented in PR-01.</summary>
public sealed class LimitBuilder
{
    /// <summary>Sets the category.</summary>
    /// <param name="category">Category.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder For(Category category) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the period.</summary>
    /// <param name="period">Period.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder Per(Period period) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the amount.</summary>
    /// <param name="amount">e.g. <c>"80.00"</c>.</param>
    /// <returns>This builder.</returns>
    public LimitBuilder Of(string amount) => throw new NotImplementedException("PR-01");

    /// <summary>Builds the limit.</summary>
    /// <returns>The limit.</returns>
    public SpendLimit Build() => throw new NotImplementedException("PR-01");

    /// <summary>Builds the limit paired with spend already made in the period.</summary>
    /// <param name="approvedSpend">e.g. <c>"30.00"</c>.</param>
    /// <returns>The limit with spend.</returns>
    public LimitWithSpend WithSpend(string approvedSpend) => throw new NotImplementedException("PR-01");
}

/// <summary>
/// Builds raw requests; defaults are valid: Ops van's number, <c>"10.00"</c>, <c>"fuel"</c>, <c>"Test Fuel Ltd"</c>,
/// <c>"2026-11-04T10:00:00Z"</c>. <c>null</c> arguments mean "field missing". Implemented in PR-01.
/// </summary>
public sealed class AuthorisationRequestBuilder
{
    /// <summary>Sets the card number.</summary>
    /// <param name="cardNumber">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithCardNumber(string? cardNumber) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the amount.</summary>
    /// <param name="amount">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithAmount(string? amount) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the category wire name.</summary>
    /// <param name="category">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithCategory(string? category) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the merchant.</summary>
    /// <param name="merchant">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithMerchant(string? merchant) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the timestamp text.</summary>
    /// <param name="timestamp">Text or null.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder WithTimestamp(string? timestamp) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the timestamp from an instant (ISO-8601 round-trip format).</summary>
    /// <param name="timestamp">Instant.</param>
    /// <returns>This builder.</returns>
    public AuthorisationRequestBuilder At(DateTimeOffset timestamp) => throw new NotImplementedException("PR-01");

    /// <summary>Builds the processor request.</summary>
    /// <returns>The request.</returns>
    public AuthorisationRequest Build() => throw new NotImplementedException("PR-01");

    /// <summary>Builds the simulate-path purchase with the same amount, category, merchant and timestamp.</summary>
    /// <returns>The purchase.</returns>
    public SimulatedPurchase BuildPurchase() => throw new NotImplementedException("PR-01");
}

/// <summary>
/// Builds <see cref="NewTransaction"/>s; default approved Fuel £10.00 "Test Fuel Ltd" at 2026-11-04T10:00:00Z, decided at
/// the same instant. Implemented in PR-01.
/// </summary>
public sealed class TransactionBuilder
{
    /// <summary>Sets the amount.</summary>
    /// <param name="amount">e.g. <c>"30.00"</c>.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder Amount(string amount) => throw new NotImplementedException("PR-01");

    /// <summary>Sets the category.</summary>
    /// <param name="category">Category.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder In(Category category) => throw new NotImplementedException("PR-01");

    /// <summary>Sets <c>OccurredAt</c> (and <c>DecidedAtUtc</c> unless set separately).</summary>
    /// <param name="occurredAt">Instant.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder At(DateTimeOffset occurredAt) => throw new NotImplementedException("PR-01");

    /// <summary>Sets <c>DecidedAtUtc</c>.</summary>
    /// <param name="decidedAtUtc">Instant.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder DecidedAt(DateTimeOffset decidedAtUtc) => throw new NotImplementedException("PR-01");

    /// <summary>Approved / <c>Approved</c> (default).</summary>
    /// <returns>This builder.</returns>
    public TransactionBuilder Approved() => throw new NotImplementedException("PR-01");

    /// <summary>Declined with a reason.</summary>
    /// <param name="reason"><c>CardFrozen</c> or <c>LimitExceeded</c>.</param>
    /// <returns>This builder.</returns>
    public TransactionBuilder Declined(Reason reason) => throw new NotImplementedException("PR-01");

    /// <summary>Builds the transaction.</summary>
    /// <returns>The transaction.</returns>
    public NewTransaction Build() => throw new NotImplementedException("PR-01");
}

/// <summary>Builds <see cref="Actor"/>s; default is the seeded admin. Implemented in PR-01.</summary>
public sealed class ActorBuilder
{
    /// <summary>The seeded admin (<see cref="Seed.AdminId"/>, "Sam Ellis").</summary>
    public static Actor Sam => throw new NotImplementedException("PR-01");

    /// <summary>Sets the name.</summary>
    /// <param name="name">Display name.</param>
    /// <returns>This builder.</returns>
    public ActorBuilder Named(string name) => throw new NotImplementedException("PR-01");

    /// <summary>Builds the actor.</summary>
    /// <returns>The actor.</returns>
    public Actor Build() => throw new NotImplementedException("PR-01");
}
