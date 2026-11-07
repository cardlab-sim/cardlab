using System.Net;
using CardLab.Domain.Application;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Limits;
using CardLab.Domain.Payments;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class AdminPaymentEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly Guid UnknownCard = Guid.Parse("00000000-0000-0000-0000-00000000dead");
    private readonly ApiFactory _fixture;

    public AdminPaymentEndpointsTests(ApiFactory fixture) => _fixture = fixture;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-19 · PR-07 · integration
    // Given Ops van with approved £120.00 and £30.50 and declined £500.00, When GET /api/cards/{id}, Then balanceOwed "150.50"; after a completed £150.50 payment (arranged through PaymentService) it is "0.00"
    // Catches: the admin not seeing the balance (AC-1, AC-2, R-1)
    [Fact]
    public async Task Card_detail_shows_balance_owed()
    {
        var api = _fixture.CreateApiClient();
        await api.ResetAsync();
        var id = await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name);
        await SeedPurchasesAsync();

        var before = await api.GetCardAsync(id.Value);
        await PayInFullAsync(id);
        var after = await api.GetCardAsync(id.Value);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal("150.50", before.Body?.BalanceOwed);
        Assert.Equal("0.00", after.Body?.BalanceOwed);
    }

    // T-I-19 · PR-07 · integration
    // Given purchases and a completed payment, When GET /api/cards/{id}/activity and ?take=2, Then newest first, the payment row {kind payment, state completed, reference, origin cardholder_view, outcome null} and purchase rows {kind purchase, outcome, category, reason}; take honoured; unknown card → 404
    // Catches: a payment listed as a purchase or the history losing 001 columns (AC-2, R-13, DEC-12)
    [Fact]
    public async Task Activity_lists_payments_as_payments()
    {
        var api = _fixture.CreateApiClient();
        await api.ResetAsync();
        var id = await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name);
        await SeedPurchasesAsync();
        var reference = await PayInFullAsync(id);

        var all = await api.ListActivityAsync(id.Value);
        var two = await api.ListActivityAsync(id.Value, 2);
        var unknown = await api.ListActivityAsync(UnknownCard);

        Assert.Equal(HttpStatusCode.OK, all.Status);
        Assert.NotNull(all.Body);
        Assert.Equal(4, all.Body.Count);
        var payment = all.Body[0];
        Assert.Equal("payment", payment.Kind);
        Assert.Equal("completed", payment.State);
        Assert.Equal(reference, payment.Reference);
        Assert.Equal("cardholder_view", payment.Origin);
        Assert.Equal("150.50", payment.Amount);
        Assert.Null(payment.Outcome);
        Assert.Null(payment.Category);
        var purchases = all.Body.Skip(1).ToList();
        Assert.All(purchases, p => Assert.Equal("purchase", p.Kind));
        Assert.All(purchases, p => Assert.Equal("travel", p.Category));
        Assert.All(purchases, p => Assert.Null(p.State));
        Assert.All(purchases, p => Assert.Null(p.Reference));
        var declined = Assert.Single(purchases, p => p.Outcome == "declined");
        Assert.Equal("500.00", declined.Amount);
        Assert.Equal("limit_exceeded", declined.Reason);
        Assert.Equal(["120.00", "30.50"], purchases.Where(p => p.Outcome == "approved").Select(p => p.Amount).Order().ToArray());
        Assert.Equal(HttpStatusCode.OK, two.Status);
        Assert.Equal(2, two.Body!.Count);
        Assert.Equal("payment", two.Body[0].Kind);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.ListActivityAsync(id.Value, 0)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.ListActivityAsync(id.Value, -1)).Status);
    }

    // T-I-19 · PR-07 · integration
    // Given an open quote, a refused reference and a completed payment, When GET /api/cards/{id}/payments, Then three items newest first with status open/refused/confirmed, state null/null/completed, origin, settledAt only for the settled one
    // Catches: refused or open references missing from the admin list (R-14)
    [Fact]
    public async Task Payments_list_includes_every_reference()
    {
        var api = _fixture.CreateApiClient();
        await api.ResetAsync();
        var id = await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name);
        await SeedPurchasesAsync();
        var completed = await PayInFullAsync(id);
        await SeedPurchasesAsync();
        var refused = await QuoteAsync(id);
        await SeedPurchasesAsync();
        using (var scope = _fixture.Services.CreateScope())
        {
            // The balance changed since the quote, so confirming it is refused.
            var result = await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(id, new PaymentReference(refused), Ct);
            Assert.False(result.IsOk);
        }

        var open = await QuoteAsync(id);

        var list = await api.ListPaymentsAsync(id.Value);
        var unknown = await api.ListPaymentsAsync(UnknownCard);

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.NotNull(list.Body);
        Assert.Equal([open, refused, completed], list.Body.Select(p => p.Reference).ToArray());
        Assert.Equal(["open", "refused", "confirmed"], list.Body.Select(p => p.Status).ToArray());
        Assert.Equal(new string?[] { null, null, "completed" }, list.Body.Select(p => p.State).ToArray());
        Assert.All(list.Body, p => Assert.Equal("cardholder_view", p.Origin));
        Assert.Equal([false, false, true], list.Body.Select(p => p.SettledAt is not null).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
    }

    private async Task SeedPurchasesAsync() =>
        await _fixture.Database.SeedTransactionsAsync(
            TestSeed.OpsVan.Name,
            [
                new TransactionBuilder().Amount("120.00").In(Category.Travel).Build(),
                new TransactionBuilder().Amount("30.50").In(Category.Travel).Build(),
                new TransactionBuilder().Amount("500.00").In(Category.Travel).Declined(Reason.LimitExceeded).Build(),
            ]);

    private async Task<string> QuoteAsync(CardId id)
    {
        using var scope = _fixture.Services.CreateScope();
        var quote = await scope.ServiceProvider.GetRequiredService<PaymentService>().QuoteAsync(id, PaymentOrigin.CardholderView, Ct);
        Assert.True(quote.IsOk);
        return quote.Value.Reference.Value;
    }

    private async Task<string> PayInFullAsync(CardId id)
    {
        var reference = await QuoteAsync(id);
        using var scope = _fixture.Services.CreateScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(id, new PaymentReference(reference), Ct);
        Assert.True(receipt.IsOk);
        Assert.Equal(PaymentState.Completed, receipt.Value.State);
        return reference;
    }
}
