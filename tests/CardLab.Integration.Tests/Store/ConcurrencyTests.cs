using System.Globalization;
using System.Net;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Domain.Ports;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using Microsoft.Extensions.DependencyInjection;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Store;

[Collection("sql")]
public sealed class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConcurrencyTests(ApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // T-I-6 · PR-08 · integration
    // Given Ops van Fuel/Day £50.00 and no spend today, When 10 POST /api/authorisations for £10.00 fuel with the same timestamp are sent in parallel (Task.WhenAll, separate HttpClients), Then exactly 5 approved and 5 declined limit_exceeded, and the approved Fuel spend for the day in the database is £50.00
    // Catches: missing or ineffective per-card lock (AC-21, R-12, DEC-4)
    [Fact]
    public async Task Ten_parallel_requests_approve_exactly_five()
    {
        await _factory.Database.SeedLimitAsync(TestSeed.OpsVan.Name, Category.Fuel, Period.Day, 50.00m);
        var id = await _factory.Database.CardIdAsync(TestSeed.OpsVan.Name);
        var at = DateTimeOffset.UtcNow;
        var request = new AuthorisationRequestBuilder()
            .WithCardNumber(TestSeed.OpsVan.CardNumber)
            .WithAmount("10.00")
            .WithCategory("fuel")
            .WithTimestamp(at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
            .Build();
        var clients = Enumerable.Range(0, 10).Select(_ => _factory.CreateApiClient()).ToList();

        var responses = await Task.WhenAll(clients.Select(c => c.AuthoriseAsync(request)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Equal(5, responses.Count(r => r.Body?.Reason == "approved"));
        Assert.Equal(5, responses.Count(r => r.Body?.Reason == "limit_exceeded"));
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICardStore>();
        var spend = await store.WithCardLockAsync(id, w => w.SumApprovedSpendAsync(Category.Fuel, PeriodCalendar.RangeContaining(at, Period.Day), Ct), Ct);
        Assert.Equal(TestMoney.Of("50.00"), spend);
    }
}
