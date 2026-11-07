using System.Net;
using System.Text.Json;
using CardLab.Api.Contracts;
using CardLab.Domain.Application;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Limits;
using CardLab.Domain.Payments;
using CardLab.Infrastructure.FakeBank;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class PaymentRecordEndpointTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public PaymentRecordEndpointTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-10 · PR-07 · integration
    // Given a completed, a failed (steered refuse) and a pending payment and a refused reference on Ops van (arranged through PaymentService from a host scope and the steering port), When GET /api/cards/{id}/payments/{reference} for each, Then quote {amount, fee "0.00", total, from, to, wording {version v1, heading, lines, button}}, acceptance {at, actor {kind cardholder, name Priya Nair}, authorityBasis, origin cardholder_view}, status, refusal fields for the refused one, payment {state, balanceBefore/After where settled, simulated true for the steered one} and events with times
    // Catches: an incomplete compliance record for any state (AC-20, R-14, OB-9)
    [Fact]
    public async Task Record_shows_shown_accepted_and_outcome_for_every_state()
    {
        var api = _fixture.CreateApiClient();
        var arranged = await ArrangeAsync(api);

        var completed = (await api.GetPaymentRecordAsync(arranged.Card.Value, arranged.Completed)).Body!;
        var failed = (await api.GetPaymentRecordAsync(arranged.Card.Value, arranged.Failed)).Body!;
        var refused = (await api.GetPaymentRecordAsync(arranged.Card.Value, arranged.Refused)).Body!;
        var pending = (await api.GetPaymentRecordAsync(arranged.Card.Value, arranged.Pending)).Body!;

        // What was shown, for every state.
        foreach (var (record, amount) in new[] { (completed, "150.50"), (failed, "150.50"), (refused, "150.50"), (pending, "301.00") })
        {
            Assert.Equal(amount, record.Quote.Amount);
            Assert.Equal("0.00", record.Quote.Fee);
            Assert.Equal(amount, record.Quote.Total);
            Assert.Equal("full", record.Quote.Kind);
            Assert.Equal("v1", record.Quote.Wording.Version);
            Assert.False(string.IsNullOrWhiteSpace(record.Quote.Wording.Heading));
            Assert.NotEmpty(record.Quote.Wording.Lines);
            Assert.False(string.IsNullOrWhiteSpace(record.Quote.Wording.Button));
            Assert.Contains(amount, string.Join(' ', record.Quote.Wording.Lines));
            Assert.Equal("Brightline Supplies Ltd current account, ending 7731", record.Quote.From);
            Assert.Equal("cardholder", record.Acceptance.Actor.Kind);
            Assert.Equal("Priya Nair", record.Acceptance.Actor.Name);
            Assert.Equal(TestSeed.AuthorityBasis, record.Acceptance.AuthorityBasis);
            Assert.Equal("cardholder_view", record.Acceptance.Origin);
            Assert.NotNull(record.Acceptance.At);
            Assert.NotEmpty(record.Events);
            Assert.All(record.Events, e => Assert.NotEqual(default, e.At));
        }

        Assert.Equal("confirmed", completed.Status);
        Assert.Equal("completed", completed.Payment!.State);
        Assert.Equal("150.50", completed.Payment.BalanceBefore);
        Assert.Equal("0.00", completed.Payment.BalanceAfter);
        Assert.False(completed.Payment.Simulated);
        Assert.NotNull(completed.Payment.SettledAt);

        Assert.Equal("confirmed", failed.Status);
        Assert.Equal("failed", failed.Payment!.State);
        Assert.Equal("insufficient_funds", failed.Payment.FailureReason);
        Assert.True(failed.Payment.Simulated);
        Assert.Equal("150.50", failed.Payment.BalanceBefore);
        Assert.Equal("150.50", failed.Payment.BalanceAfter);

        Assert.Equal("refused", refused.Status);
        Assert.Null(refused.Payment);
        Assert.Equal("balance_changed", refused.RefusalReason);
        Assert.Equal("301.00", refused.BalanceAtRefusal);
        Assert.Contains(refused.Events, e => e.Kind == "confirm_refused");

        Assert.Equal("confirmed", pending.Status);
        Assert.Equal("pending", pending.Payment!.State);
        Assert.Null(pending.Payment.SettledAt);
        Assert.Null(pending.Payment.BalanceBefore);
        Assert.Null(pending.Payment.BalanceAfter);
    }

    // T-I-10 · PR-07 · integration
    // Given those records, When a status check, a repeated confirm and a host restart (CreateRestartedHost on the same database) happen, Then each record's quote and acceptance parts are byte-for-byte unchanged and only events/payment state grew
    // Catches: the shown-and-accepted part being rewritten (AC-20, R-14)
    [Fact]
    public async Task Shown_part_never_changes()
    {
        var api = _fixture.CreateApiClient();
        var arranged = await ArrangeAsync(api);
        var references = new[] { arranged.Completed, arranged.Failed, arranged.Refused, arranged.Pending };
        var before = new Dictionary<string, PaymentRecordDto>();
        foreach (var reference in references)
        {
            before[reference] = (await api.GetPaymentRecordAsync(arranged.Card.Value, reference)).Body!;
        }

        await _fixture.Services.CreateScope().ServiceProvider.GetRequiredService<PaymentService>().CheckStatusAsync(arranged.Card, new PaymentReference(arranged.Pending), Ct);
        foreach (var reference in references)
        {
            using var scope = _fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(arranged.Card, new PaymentReference(reference), Ct);
        }

        await using var restarted = _fixture.CreateRestartedHost();
        var restartedApi = restarted.CreateApiClient();
        foreach (var reference in references)
        {
            var after = (await restartedApi.GetPaymentRecordAsync(arranged.Card.Value, reference)).Body!;
            Assert.Equal(Serialize(before[reference].Quote), Serialize(after.Quote));
            Assert.Equal(Serialize(before[reference].Acceptance), Serialize(after.Acceptance));
            Assert.True(after.Events.Count >= before[reference].Events.Count);
            Assert.Equal(before[reference].Events, after.Events.Take(before[reference].Events.Count).ToList());
        }

        var pendingAfter = (await restartedApi.GetPaymentRecordAsync(arranged.Card.Value, arranged.Pending)).Body!;
        Assert.Equal("completed", pendingAfter.Payment!.State);
        Assert.True(pendingAfter.Events.Count > before[arranged.Pending].Events.Count);
        Assert.Equal(before[arranged.Pending].Acceptance.At, pendingAfter.Acceptance.At);
    }

    // T-I-10 · PR-07 · integration
    // Given another card's reference, a malformed reference and an unknown card, When GET the record, Then 404
    // Catches: records leaking across cards (design §10)
    [Fact]
    public async Task Record_of_another_card_is_not_found()
    {
        var api = _fixture.CreateApiClient();
        var arranged = await ArrangeAsync(api);
        var otherCard = (await _fixture.Database.CardIdAsync(TestSeed.Marketing.Name)).Value;

        var other = await api.GetPaymentRecordAsync(otherCard, arranged.Completed);
        var malformed = await api.GetPaymentRecordAsync(arranged.Card.Value, "pay-1234");
        var unknownReference = await api.GetPaymentRecordAsync(arranged.Card.Value, "PAY-00000000");
        var unknownCard = await api.GetPaymentRecordAsync(Guid.Parse("00000000-0000-0000-0000-00000000dead"), arranged.Completed);
        var own = await api.GetPaymentRecordAsync(arranged.Card.Value, arranged.Completed);

        Assert.Equal(HttpStatusCode.NotFound, other.Status);
        Assert.Equal(HttpStatusCode.NotFound, malformed.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknownReference.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknownCard.Status);
        Assert.Equal(HttpStatusCode.OK, own.Status);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private async Task<Arranged> ArrangeAsync(ApiClient api)
    {
        await api.ResetAsync();
        var card = await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name);

        await SeedPurchaseAsync();
        var completed = await ConfirmAsync(card, await QuoteAsync(card));

        await SeedPurchaseAsync();
        await SteerAsync(NextDebitOutcome.RefuseInsufficientFunds);
        var failed = await ConfirmAsync(card, await QuoteAsync(card));

        var refused = await QuoteAsync(card);
        await SeedPurchaseAsync();
        using (var scope = _fixture.Services.CreateScope())
        {
            var refusal = await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(card, new PaymentReference(refused), Ct);
            Assert.False(refusal.IsOk);
        }

        await SteerAsync(NextDebitOutcome.TimeoutDebitApplied);
        var pending = await ConfirmAsync(card, await QuoteAsync(card));
        return new Arranged(card, completed, failed, refused, pending);
    }

    private async Task SeedPurchaseAsync() =>
        await _fixture.Database.SeedTransactionsAsync(
            TestSeed.OpsVan.Name,
            [
                new TransactionBuilder().Amount("120.00").In(Category.Travel).Build(),
                new TransactionBuilder().Amount("30.50").In(Category.Travel).Build(),
                new TransactionBuilder().Amount("500.00").In(Category.Travel).Declined(Reason.LimitExceeded).Build(),
            ]);

    private async Task SteerAsync(NextDebitOutcome outcome)
    {
        using var scope = _fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFakeBankSteering>().SetAsync(new Steering(outcome, null), Ct);
    }

    private async Task<string> QuoteAsync(CardId card)
    {
        using var scope = _fixture.Services.CreateScope();
        var quote = await scope.ServiceProvider.GetRequiredService<PaymentService>().QuoteAsync(card, PaymentOrigin.CardholderView, Ct);
        Assert.True(quote.IsOk);
        return quote.Value.Reference.Value;
    }

    private async Task<string> ConfirmAsync(CardId card, string reference)
    {
        using var scope = _fixture.Services.CreateScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<PaymentService>().ConfirmAsync(card, new PaymentReference(reference), Ct);
        Assert.True(receipt.IsOk);
        return reference;
    }

    private sealed record Arranged(CardId Card, string Completed, string Failed, string Refused, string Pending);
}
