using CardLab.Domain.Payments;
using CardLab.TestKit;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Payments;

public sealed class QuoteRulesTests
{
    // T-U-9 · PR-01 · unit
    // Given Ops van (0001), the seeded linked account (7731), owed £150.50, the cardholder actor Priya Nair, origin DeploySmoke and now, When QuoteRules.Create, Then kind Full, amount £150.50, fee £0.00, total £150.50, FromLabel "Brightline Supplies Ltd current account, ending 7731", ToLabel "Ops van card, ending 0001", the account's authority basis, CreatedAtUtc = now, actor and origin as given, and Shown equal to WordingV1.ReviewScreen(£150.50, FromLabel, ToLabel)
    // Catches: stored fields disagreeing with the wording shown, or a non-zero fee (AC-6, R-4, R-14, OB-9)
    [Fact]
    public void Creates_full_balance_quote_with_zero_fee_and_v1_wording()
    {
        var card = CardBuilder.From(Seed.OpsVan).BuildRef();
        var account = new LinkedAccountBuilder().Build();
        var actor = new PaymentActor(ActorKind.Cardholder, Guid.Parse("0b0b0b0b-0000-4000-8000-000000000001"), "Priya Nair");
        var now = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        var quote = QuoteRules.Create(new PaymentReference("PAY-7K3M9Q2T"), card, "Ops van", account, TestMoney.Of("150.50"), actor, PaymentOrigin.DeploySmoke, now);

        Assert.Equal(new PaymentReference("PAY-7K3M9Q2T"), quote.Reference);
        Assert.Equal(card.Id, quote.Card);
        Assert.Equal(PaymentKind.Full, quote.Kind);
        Assert.Equal(TestMoney.Of("150.50"), quote.Amount);
        Assert.Equal(Money.Zero, quote.Fee);
        Assert.Equal(TestMoney.Of("150.50"), quote.Total);
        Assert.Equal("Brightline Supplies Ltd current account, ending 7731", quote.FromLabel);
        Assert.Equal("Ops van card, ending 0001", quote.ToLabel);
        Assert.Equal(Seed.AuthorityBasis, quote.AuthorityBasis);
        Assert.Equal(now, quote.CreatedAtUtc);
        Assert.Equal(actor, quote.Actor);
        Assert.Equal(PaymentOrigin.DeploySmoke, quote.Origin);
        var expectedShown = WordingV1.ReviewScreen(TestMoney.Of("150.50"), "Brightline Supplies Ltd current account, ending 7731", "Ops van card, ending 0001");
        Assert.Equal(expectedShown.Version, quote.Shown.Version);
        Assert.Equal(expectedShown.Heading, quote.Shown.Heading);
        Assert.Equal(expectedShown.Lines, quote.Shown.Lines);
        Assert.Equal(expectedShown.ButtonLabel, quote.Shown.ButtonLabel);
    }
}
