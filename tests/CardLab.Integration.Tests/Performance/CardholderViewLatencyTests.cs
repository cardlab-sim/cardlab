using System.Diagnostics;
using System.Net;
using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;
using CardLab.TestKit.Builders;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Performance;

[Collection("sql")]
public sealed class CardholderViewLatencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public CardholderViewLatencyTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-17 · PR-06 · integration
    // Given Ops van with 1,000 transactions (SeedTransactionsAsync) and 200 payments in mixed states, When GET /api/cardholder/cards/{id} 50 times after 5 warm-up calls, Then p95 ≤ 300 ms
    // Catches: unindexed balance or activity queries (NFR-2, design §13)
    [Fact]
    public async Task Cardholder_view_p95_within_300_ms()
    {
        var client = _fixture.CreateApiClient();
        await client.ResetAsync();
        var db = _fixture.Database;
        var id = (await db.CardIdAsync(TestSeed.OpsVan.Name)).Value;
        var now = DateTimeOffset.UtcNow;
        var history = Enumerable.Range(0, 1000).Select(i =>
        {
            var at = now.AddMinutes(-(i * 57.6));
            return new TransactionBuilder().Amount("1.00").At(at).DecidedAt(at).Build();
        });
        await db.SeedTransactionsAsync(TestSeed.OpsVan.Name, history);
        await db.ExecuteAsync(SeedPaymentsSql, new Dictionary<string, object?> { ["@card"] = id, ["@now"] = now.UtcDateTime });
        Assert.Equal(200, await db.CountRowsAsync("Payment"));

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetCardholderViewAsync(id)).Status);
        }

        var samples = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var started = Stopwatch.GetTimestamp();
            var view = await client.GetCardholderViewAsync(id);
            samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(HttpStatusCode.OK, view.Status);
        }

        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        Assert.True(p95 <= 300, $"p95 cardholder view latency was {p95:F1} ms (budget 300 ms)");
    }

    // 200 quotes with payments: payment 1 (newest) pending, the rest alternating completed and failed.
    private const string SeedPaymentsSql = """
        DECLARE @account uniqueidentifier = (SELECT TOP 1 [Id] FROM [LinkedAccount]);
        WITH numbers AS (
            SELECT TOP 200 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
        INSERT INTO [PaymentQuote] ([Reference], [CardId], [LinkedAccountId], [Kind], [Amount], [Fee], [Total], [FromLabel], [ToLabel],
            [WordingVersion], [Heading], [RenderedLines], [ButtonLabel], [ActorKind], [ActorId], [ActorName], [AuthorityBasis], [Origin],
            [CreatedAtUtc], [Status])
        SELECT 'PAY-' + RIGHT('00000000' + CAST(n AS varchar(8)), 8), @card, @account, 'Full', 1.00, 0.00, 1.00, 'From', 'To',
            'v1', 'Pay your full balance', 'Amount: £1.00', 'Confirm payment of £1.00', 'Cardholder', @card, 'Priya Nair', 'Authority', 'CardholderView',
            DATEADD(minute, -n, @now), 'Confirmed'
        FROM numbers;
        WITH numbers AS (
            SELECT TOP 200 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
        INSERT INTO [Payment] ([Id], [Reference], [CardId], [Kind], [Amount], [Fee], [State], [CreatedAtUtc], [SettledAtUtc], [EffectiveAtUtc], [FailureReason], [BalanceBefore], [BalanceAfter], [Simulated])
        SELECT NEWID(), 'PAY-' + RIGHT('00000000' + CAST(n AS varchar(8)), 8), @card, 'Full', 1.00, 0.00,
            CASE WHEN n = 1 THEN 'Pending' WHEN n % 2 = 0 THEN 'Completed' ELSE 'Failed' END,
            DATEADD(minute, -n, @now),
            CASE WHEN n = 1 THEN NULL ELSE DATEADD(minute, -n, @now) END,
            CASE WHEN n = 1 THEN NULL ELSE DATEADD(minute, -n, @now) END,
            CASE WHEN n > 1 AND n % 2 = 1 THEN 'AccountUnavailable' END,
            CASE WHEN n = 1 THEN NULL ELSE 1.00 END,
            CASE WHEN n = 1 THEN NULL WHEN n % 2 = 0 THEN 0.00 ELSE 1.00 END,
            0
        FROM numbers;
        """;
}
