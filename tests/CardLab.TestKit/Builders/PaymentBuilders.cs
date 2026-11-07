using System.Globalization;
using CardLab.Domain;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.TestKit.Builders;

// 002 payment builders (design §15.2). Complete in PR-00: plain test data with valid defaults (the seeded Ops van card,
// £150.50, 2026-11-07T10:00Z), shared by PR-01, PR-02, PR-04 and PR-05. They contain no rules: the default Shown
// wording is plausible W-1 text formatted locally, NOT the authority; tests that assert exact wording use WordingV1.

/// <summary>Builds a valid <see cref="Quote"/> (defaults: PAY-00000001, Ops van, £150.50, fee £0.00, cardholder Priya Nair, origin cardholder view).</summary>
public sealed class QuoteBuilder
{
    /// <summary>Default quote time.</summary>
    public static readonly DateTimeOffset DefaultAt = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

    private PaymentReference reference = new("PAY-00000001");
    private CardId card = new(Guid.Parse("0c0c0c0c-0000-4000-8000-000000000001"));
    private string cardName = Seed.OpsVan.Name;
    private string cardLastFour = Seed.OpsVan.LastFour;
    private string cardholder = Seed.OpsVan.CardholderName;
    private Money amount = TestMoney.Of("150.50");
    private DateTimeOffset at = DefaultAt;
    private PaymentOrigin origin = PaymentOrigin.CardholderView;

    /// <summary>Sets the reference.</summary>
    /// <param name="value">e.g. "PAY-00000002".</param>
    /// <returns>This builder.</returns>
    public QuoteBuilder WithReference(string value)
    {
        reference = new PaymentReference(value);
        return this;
    }

    /// <summary>Targets a seeded store card (id, name, last four, cardholder).</summary>
    /// <param name="seed">Card from <c>CardBuilder.Build()</c>.</param>
    /// <returns>This builder.</returns>
    public QuoteBuilder ForCard(StoreSeedCard seed)
    {
        card = seed.Id;
        cardName = seed.Name;
        cardLastFour = seed.LastFour;
        cardholder = seed.CardholderName;
        return this;
    }

    /// <summary>Sets the amount (total follows; fee stays £0.00).</summary>
    /// <param name="value">e.g. "170.50".</param>
    /// <returns>This builder.</returns>
    public QuoteBuilder WithAmount(string value)
    {
        amount = TestMoney.Of(value);
        return this;
    }

    /// <summary>Sets the quote time.</summary>
    /// <param name="value">Instant.</param>
    /// <returns>This builder.</returns>
    public QuoteBuilder At(DateTimeOffset value)
    {
        at = value;
        return this;
    }

    /// <summary>Sets the origin.</summary>
    /// <param name="value">Origin.</param>
    /// <returns>This builder.</returns>
    public QuoteBuilder WithOrigin(PaymentOrigin value)
    {
        origin = value;
        return this;
    }

    /// <summary>Builds the quote.</summary>
    /// <returns>The quote.</returns>
    public Quote Build()
    {
        var from = $"{Seed.LinkedAccountName}, ending {Seed.LinkedAccountLastFour}";
        var to = $"{cardName} card, ending {cardLastFour}";
        var shown = Display(amount);
        var wording = new RenderedWording(
            "v1",
            "Pay your full balance",
            [$"Amount: {shown}", "Fee: £0.00 (no fee)", $"Total to pay: {shown}", "When: (test data)", $"From: {from}", $"To: {to}"],
            $"Confirm payment of {shown}");
        return new Quote(
            reference,
            card,
            PaymentKind.Full,
            amount,
            Money.Zero,
            amount,
            from,
            to,
            new PaymentActor(ActorKind.Cardholder, card.Value, cardholder),
            Seed.AuthorityBasis,
            at,
            wording,
            origin);
    }

    private static string Display(Money money) => "£" + money.Amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
}

/// <summary>Builds payments in any state: <see cref="NewPayment"/>, <see cref="PaymentSnapshot"/> and <see cref="Settlement"/>.</summary>
public sealed class PaymentBuilder
{
    private PaymentId id = new(Guid.Parse("0a0a0a0a-0000-4000-8000-000000000001"));
    private PaymentReference reference = new("PAY-00000001");
    private Money amount = TestMoney.Of("150.50");
    private DateTimeOffset createdAt = QuoteBuilder.DefaultAt.AddMinutes(1);
    private Settlement? settlement;

    /// <summary>Copies reference and amount from a quote.</summary>
    /// <param name="quote">The quote.</param>
    /// <returns>This builder.</returns>
    public PaymentBuilder ForQuote(Quote quote)
    {
        reference = quote.Reference;
        amount = quote.Amount;
        return this;
    }

    /// <summary>Sets the id (snapshots only).</summary>
    /// <param name="value">Id.</param>
    /// <returns>This builder.</returns>
    public PaymentBuilder WithId(Guid value)
    {
        id = new PaymentId(value);
        return this;
    }

    /// <summary>Sets the acceptance time.</summary>
    /// <param name="value">Instant.</param>
    /// <returns>This builder.</returns>
    public PaymentBuilder CreatedAt(DateTimeOffset value)
    {
        createdAt = value;
        return this;
    }

    /// <summary>Completed: settled one minute after creation, effective = bank debit time = settle time.</summary>
    /// <param name="before">Balance before (decimal text).</param>
    /// <param name="after">Balance after.</param>
    /// <param name="simulated">Steered outcome.</param>
    /// <returns>This builder.</returns>
    public PaymentBuilder Completed(string before = "150.50", string after = "0.00", bool simulated = false)
    {
        var settled = createdAt.AddMinutes(1);
        settlement = new Settlement(PaymentState.Completed, null, settled, settled, settled, TestMoney.Of(before), TestMoney.Of(after), simulated);
        return this;
    }

    /// <summary>Failed with a reason; balance before = after.</summary>
    /// <param name="reason">Reason.</param>
    /// <param name="balance">Balance owed (unchanged).</param>
    /// <param name="simulated">Steered outcome.</param>
    /// <returns>This builder.</returns>
    public PaymentBuilder Failed(FailureReason reason, string balance = "150.50", bool simulated = false)
    {
        var settled = createdAt.AddMinutes(1);
        settlement = new Settlement(PaymentState.Failed, reason, settled, settled, null, TestMoney.Of(balance), TestMoney.Of(balance), simulated);
        return this;
    }

    /// <summary>The payment to create (state Pending).</summary>
    /// <returns>The new payment.</returns>
    public NewPayment BuildNew() => new(reference, PaymentKind.Full, amount, Money.Zero, createdAt);

    /// <summary>The settlement set by <see cref="Completed"/> / <see cref="Failed"/>.</summary>
    /// <returns>The settlement.</returns>
    /// <exception cref="InvalidOperationException">Neither was called.</exception>
    public Settlement BuildSettlement() => settlement ?? throw new InvalidOperationException("Call Completed() or Failed() first.");

    /// <summary>A snapshot in the built state (Pending unless settled).</summary>
    /// <returns>The snapshot.</returns>
    public PaymentSnapshot BuildSnapshot() => new(id, reference, settlement?.State ?? PaymentState.Pending, amount, createdAt, settlement);
}

/// <summary>Builds the linked account (defaults: the seeded "Brightline Supplies Ltd current account", 7731).</summary>
public sealed class LinkedAccountBuilder
{
    private Guid id = Guid.Parse("1a1a1a1a-0000-4000-8000-000000000731");
    private string lastFour = Seed.LinkedAccountLastFour;
    private BankAccountRef bankRef = new(Seed.BankAccountRef);

    /// <summary>Sets the row id.</summary>
    /// <param name="value">Id.</param>
    /// <returns>This builder.</returns>
    public LinkedAccountBuilder WithId(Guid value)
    {
        id = value;
        return this;
    }

    /// <summary>Sets the last four.</summary>
    /// <param name="value">Four digits.</param>
    /// <returns>This builder.</returns>
    public LinkedAccountBuilder WithLastFour(string value)
    {
        lastFour = value;
        return this;
    }

    /// <summary>Sets the bank reference.</summary>
    /// <param name="value">Bank-side id.</param>
    /// <returns>This builder.</returns>
    public LinkedAccountBuilder WithBankRef(Guid value)
    {
        bankRef = new BankAccountRef(value);
        return this;
    }

    /// <summary>Builds the account.</summary>
    /// <returns>The account.</returns>
    public LinkedAccount Build() => new(id, Seed.LinkedAccountName, lastFour, bankRef, Seed.AuthorityBasis);
}

/// <summary>Short factories for bank outcomes (not simulated unless stated).</summary>
public static class BankOutcomes
{
    /// <summary>Completed.</summary>
    /// <param name="debitedAtUtc">Bank debit time, or null.</param>
    /// <param name="simulated">Steered.</param>
    /// <returns>The outcome.</returns>
    public static BankOutcome Completed(DateTimeOffset? debitedAtUtc = null, bool simulated = false) => new BankOutcome.Completed(debitedAtUtc, simulated);

    /// <summary>Refused: insufficient funds.</summary>
    /// <param name="simulated">Steered.</param>
    /// <returns>The outcome.</returns>
    public static BankOutcome InsufficientFunds(bool simulated = false) => new BankOutcome.Refused(FailureReason.InsufficientFunds, simulated);

    /// <summary>Refused: account unavailable.</summary>
    /// <param name="simulated">Steered.</param>
    /// <returns>The outcome.</returns>
    public static BankOutcome AccountUnavailable(bool simulated = false) => new BankOutcome.Refused(FailureReason.AccountUnavailable, simulated);

    /// <summary>Status check: not received.</summary>
    /// <param name="simulated">Steered.</param>
    /// <returns>The outcome.</returns>
    public static BankOutcome NotReceived(bool simulated = false) => new BankOutcome.NotReceived(simulated);

    /// <summary>No answer in time.</summary>
    /// <param name="simulated">Steered.</param>
    /// <returns>The outcome.</returns>
    public static BankOutcome Unknown(bool simulated = false) => new BankOutcome.Unknown(simulated);
}
