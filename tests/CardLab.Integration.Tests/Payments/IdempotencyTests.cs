using System.Net;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class IdempotencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public IdempotencyTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-4 · PR-06 · integration
    // Given a completed confirm, When the same confirm is sent twice more, Then each 200 body equals the first except repeated=true; one Payment row; one BankEntry; the stored record (ICardStore.GetPaymentRecordAsync from a host scope) lists two ConfirmRepeated events
    // Catches: a resubmit creating a second payment or debit (AC-9, R-7)
    [Fact]
    public async Task Repeated_confirm_returns_the_original_receipt()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var reference = (await client.CreateQuoteAsync(id)).Body!.Reference;
        var first = await client.ConfirmAsync(id, reference);

        var second = await client.ConfirmAsync(id, reference);
        var third = await client.ConfirmAsync(id, reference);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.False(first.Body!.Repeated);
        foreach (var repeat in new[] { second, third })
        {
            Assert.Equal(HttpStatusCode.OK, repeat.Status);
            Assert.True(repeat.Body!.Repeated);
            Assert.Equal(first.Body with { Repeated = true, Time = default }, repeat.Body with { Time = default });
            Assert.True((first.Body.Time - repeat.Body.Time).Duration() < TimeSpan.FromMilliseconds(1), "the repeated receipt time is the stored time (the store keeps milliseconds)");
        }

        Assert.Equal(1, await _fixture.Database.CountRowsAsync("Payment"));
        Assert.Equal(1, await _fixture.Database.CountRowsAsync("BankEntry"));
        using var scope = _fixture.Services.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<ICardStore>().GetPaymentRecordAsync(new PaymentReference(reference), TestContext.Current.CancellationToken);
        Assert.Equal(2, record!.Events.Count(e => e.Kind == PaymentEventKind.ConfirmRepeated));
    }

    // T-I-5 · PR-06 · integration
    // Given one quote, When 20 confirms of the same reference are sent in parallel, Then all return 200 with state completed and the same reference, exactly one Payment row and one BankEntry exist
    // Catches: concurrent confirms racing past the rules or the unique indexes (AC-10, DEC-5)
    [Fact]
    public async Task Parallel_confirms_create_one_payment()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var id = (await _fixture.Database.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        await client.SimulatePurchaseAsync(id, new SimulatedPurchase("150.50", "fuel", "Shell", null));
        var reference = (await client.CreateQuoteAsync(id)).Body!.Reference;

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.ConfirmAsync(id, reference)));

        Assert.All(responses, r =>
        {
            Assert.Equal(HttpStatusCode.OK, r.Status);
            Assert.Equal("completed", r.Body!.State);
            Assert.Equal(reference, r.Body.Reference);
        });
        Assert.Equal(1, await _fixture.Database.CountRowsAsync("Payment"));
        Assert.Equal(1, await _fixture.Database.CountRowsAsync("BankEntry"));
    }
}
