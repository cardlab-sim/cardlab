using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Infrastructure.Model;

namespace CardLab.Integration.Tests.Harness;

/// <summary>
/// A fresh database per test class (design §15.2): <c>CardLab_Test_&lt;guid&gt;</c> on the server of
/// <c>CARDLAB_TEST_CONNECTION</c> (default <c>Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True</c>),
/// migrated and seeded with the standard seed (dev lookup key) on <see cref="InitializeAsync"/>, dropped on
/// <see cref="DisposeAsync"/>. Arrange helpers write rows directly so a test can set up state without the endpoint
/// it is not testing. Implemented in PR-06.
/// </summary>
public sealed class SqlTestDatabase : IAsyncLifetime
{
    /// <summary>Connection string of this test database.</summary>
    public string ConnectionString => throw new NotImplementedException("PR-06");

    /// <summary>Creates, migrates and seeds the database.</summary>
    /// <returns>A task.</returns>
    public ValueTask InitializeAsync() => throw new NotImplementedException("PR-06");

    /// <summary>Drops the database.</summary>
    /// <returns>A task.</returns>
    public ValueTask DisposeAsync() => throw new NotImplementedException("PR-06");

    /// <summary>A new context on this database (caller disposes).</summary>
    /// <returns>The context.</returns>
    public CardLabDbContext CreateDbContext() => throw new NotImplementedException("PR-06");

    /// <summary>Id of a seeded card by name (e.g. <c>Seed.OpsVan.Name</c>).</summary>
    /// <param name="cardName">Card name.</param>
    /// <returns>The id.</returns>
    public Task<CardId> CardIdAsync(string cardName) => throw new NotImplementedException("PR-06");

    /// <summary>Sets a card's status directly (arrange only).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="status">Status.</param>
    /// <returns>A task.</returns>
    public Task SetStatusAsync(string cardName, CardStatus status) => throw new NotImplementedException("PR-06");

    /// <summary>Inserts or replaces a limit directly (arrange only).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="amount">Amount.</param>
    /// <returns>A task.</returns>
    public Task SeedLimitAsync(string cardName, Category category, Period period, decimal amount) => throw new NotImplementedException("PR-06");

    /// <summary>Inserts transactions directly (arrange only; e.g. 1,000 rows for T-I-14).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="transactions">Rows to insert.</param>
    /// <returns>A task.</returns>
    public Task SeedTransactionsAsync(string cardName, IEnumerable<NewTransaction> transactions) => throw new NotImplementedException("PR-06");

    /// <summary>Total rows in <c>Transaction</c> (all cards).</summary>
    /// <returns>Row count.</returns>
    public Task<int> CountTransactionsAsync() => throw new NotImplementedException("PR-06");

    /// <summary>Total rows in <c>AuditEvent</c> (all cards).</summary>
    /// <returns>Row count.</returns>
    public Task<int> CountAuditEventsAsync() => throw new NotImplementedException("PR-06");

    /// <summary>Every row of every table rendered as text (column=value; binary as hex), for <c>PanScanner</c> (AC-24).</summary>
    /// <returns>The dump.</returns>
    public Task<string> DumpAllRowsAsync() => throw new NotImplementedException("PR-06");
}
