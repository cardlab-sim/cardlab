using System.Diagnostics;
using System.Globalization;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Performance;

[Collection("sql")]
public sealed class DecisionLatencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DecisionLatencyTests(ApiFactory factory) => _factory = factory;

    // T-I-14 · PR-08 · integration (performance)
    // Given Ops van with 1,000 stored transactions spread over the last 40 days and limits in fuel, travel and other (day, week, month), When 200 sequential POST /api/authorisations across those categories are timed after 10 warm-up requests, Then the 95th percentile is at most 300 ms
    // Catches: unindexed spend sums or N+1 queries on the hot path (AC-35, NFR-5)
    [Fact]
    public async Task P95_decision_latency_is_within_300_ms()
    {
        var db = _factory.Database;
        Category[] categories = [Category.Fuel, Category.Travel, Category.Other];
        var now = DateTimeOffset.UtcNow;
        foreach (var category in categories)
        {
            foreach (var period in Enum.GetValues<Period>())
            {
                await db.SeedLimitAsync(TestSeed.OpsVan.Name, category, period, 100000.00m);
            }
        }

        var history = Enumerable.Range(0, 1000).Select(i =>
        {
            var at = now.AddMinutes(-(i * 57.6));
            return new TransactionBuilder().Amount("1.00").In(categories[i % 3]).At(at).DecidedAt(at).Build();
        });
        await db.SeedTransactionsAsync(TestSeed.OpsVan.Name, history);

        var client = _factory.CreateApiClient();
        var timestamp = now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string[] wire = ["fuel", "travel", "other"];

        async Task<double> TimedAsync(int i)
        {
            var request = new AuthorisationRequestBuilder().WithCardNumber(TestSeed.OpsVan.CardNumber).WithAmount("0.01").WithCategory(wire[i % 3]).WithTimestamp(timestamp).Build();
            var started = Stopwatch.GetTimestamp();
            var response = await client.AuthoriseAsync(request);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.Equal("approved", response.Body?.Outcome);
            return elapsed;
        }

        for (var i = 0; i < 10; i++)
        {
            await TimedAsync(i);
        }

        var samples = new List<double>();
        for (var i = 0; i < 200; i++)
        {
            samples.Add(await TimedAsync(i));
        }

        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        Assert.True(p95 <= 300, $"p95 decision latency was {p95:F1} ms (budget 300 ms)");
    }
}
