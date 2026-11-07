using System.Text;
using CardLab.Domain.Cards;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.Infrastructure.Lookup;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using CardLab.Infrastructure.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TestSeed = CardLab.TestKit.Seed;

namespace CardLab.Integration.Tests.Harness;

/// <summary>
/// A fresh database per test class (design §15.2): <c>CardLab_Test_&lt;guid&gt;</c> on the server of
/// <c>CARDLAB_TEST_CONNECTION</c> (default <c>Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True</c>),
/// migrated and seeded with the standard seed (dev lookup key) on <see cref="InitializeAsync"/>, dropped on
/// <see cref="DisposeAsync"/>. Arrange helpers write rows directly so a test can set up state without the endpoint
/// it is not testing.
/// </summary>
public sealed class SqlTestDatabase : IAsyncLifetime
{
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _databaseName = "CardLab_Test_" + Guid.NewGuid().ToString("N");
    private readonly SqlConnectionStringBuilder _server;

    /// <summary>Creates the fixture; the database itself is created by <see cref="InitializeAsync"/>.</summary>
    public SqlTestDatabase()
    {
        var configured = Environment.GetEnvironmentVariable("CARDLAB_TEST_CONNECTION");
        _server = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(configured) ? DefaultServer : configured);
    }

    /// <summary>Connection string of this test database.</summary>
    public string ConnectionString => new SqlConnectionStringBuilder(_server.ConnectionString) { InitialCatalog = _databaseName }.ConnectionString;

    /// <summary>Creates, migrates and seeds the database.</summary>
    /// <returns>A task.</returns>
    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CardLabDbContext>(o => o.UseSqlServer(ConnectionString));
        services.Configure<LookupOptions>(o => o.LookupKey = TestSeed.DevLookupKey);
        services.Configure<SeedOptions>(o =>
        {
            o.AccountName = TestSeed.AccountName;
            o.TimeZoneId = TestSeed.TimeZoneId;
            o.AdminId = TestSeed.AdminId;
            o.AdminName = TestSeed.AdminName;
            o.Cards = TestSeed.Cards.Select(c => new SeedCardOptions { Name = c.Name, CardholderName = c.CardholderName, CardNumber = c.CardNumber }).ToList();
            o.LinkedAccount = new SeedLinkedAccountOptions
            {
                DisplayName = TestSeed.LinkedAccountName,
                AccountNumber = TestSeed.LinkedAccountNumber,
                BankAccountRef = TestSeed.BankAccountRef,
                OpeningBalance = TestSeed.BankOpeningBalance,
                AuthorityBasis = TestSeed.AuthorityBasis,
            };
        });
        services.AddSingleton<CardLab.Domain.Ports.ICardNumberLookup, HmacCardNumberLookup>();
        await using var provider = services.BuildServiceProvider();
        await provider.InitialiseDatabaseAsync();
    }

    /// <summary>Drops the database.</summary>
    /// <returns>A task.</returns>
    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_server.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>A new context on this database (caller disposes).</summary>
    /// <returns>The context.</returns>
    public CardLabDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<CardLabDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Id of a seeded card by name (e.g. <c>Seed.OpsVan.Name</c>).</summary>
    /// <param name="cardName">Card name.</param>
    /// <returns>The id.</returns>
    public async Task<CardId> CardIdAsync(string cardName)
    {
        await using var db = CreateDbContext();
        return new CardId(await db.Cards.Where(c => c.Name == cardName).Select(c => c.Id).SingleAsync());
    }

    /// <summary>Sets a card's status directly (arrange only).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="status">Status.</param>
    /// <returns>A task.</returns>
    public async Task SetStatusAsync(string cardName, CardStatus status)
    {
        await using var db = CreateDbContext();
        var statusName = status.ToString();
        var updated = await db.Cards.Where(c => c.Name == cardName).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, statusName));
        if (updated != 1)
        {
            throw new InvalidOperationException($"Expected one card named '{cardName}', updated {updated}.");
        }
    }

    /// <summary>Inserts or replaces a limit directly (arrange only).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="category">Category.</param>
    /// <param name="period">Period.</param>
    /// <param name="amount">Amount.</param>
    /// <returns>A task.</returns>
    public async Task SeedLimitAsync(string cardName, Category category, Period period, decimal amount)
    {
        await using var db = CreateDbContext();
        var cardId = (await CardIdAsync(cardName)).Value;
        var categoryName = category.ToString();
        var periodName = period.ToString();
        var existing = await db.SpendLimits.SingleOrDefaultAsync(l => l.CardId == cardId && l.Category == categoryName && l.Period == periodName);
        if (existing is null)
        {
            db.SpendLimits.Add(new SpendLimitRow { Id = Guid.NewGuid(), CardId = cardId, Category = categoryName, Period = periodName, Amount = amount });
        }
        else
        {
            existing.Amount = amount;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Inserts transactions directly (arrange only; e.g. 1,000 rows for T-I-14).</summary>
    /// <param name="cardName">Card name.</param>
    /// <param name="transactions">Rows to insert.</param>
    /// <returns>A task.</returns>
    public async Task SeedTransactionsAsync(string cardName, IEnumerable<NewTransaction> transactions)
    {
        await using var db = CreateDbContext();
        var cardId = await CardIdAsync(cardName);
        db.Transactions.AddRange(transactions.Select(t => RowMapper.ToRow(cardId, t)));
        await db.SaveChangesAsync();
    }

    /// <summary>Total rows in <c>Transaction</c> (all cards).</summary>
    /// <returns>Row count.</returns>
    public async Task<int> CountTransactionsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Transactions.CountAsync();
    }

    /// <summary>Total rows in <c>AuditEvent</c> (all cards).</summary>
    /// <returns>Row count.</returns>
    public async Task<int> CountAuditEventsAsync()
    {
        await using var db = CreateDbContext();
        return await db.AuditEvents.CountAsync();
    }

    /// <summary>002: number of rows in a table (e.g. <c>Payment</c>, <c>BankEntry</c>), by raw SQL so it works for any table.</summary>
    /// <param name="table">Table name: letters only.</param>
    /// <returns>The count.</returns>
    /// <exception cref="ArgumentException">The name is not a plain identifier.</exception>
    public async Task<int> CountRowsAsync(string table)
    {
        if (table.Length == 0 || !table.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Table name must be letters only.", nameof(table));
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{table}]";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>002: runs arrange SQL directly (e.g. inserting payment rows when the endpoints under test must not be used).</summary>
    /// <param name="sql">The statement(s); test-authored text only.</param>
    /// <param name="parameters">Named parameters (<c>@name</c> to value).</param>
    /// <returns>Rows affected.</returns>
    public async Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters ?? new Dictionary<string, object?>())
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Every row of every table rendered as text (column=value), for <c>PanScanner</c> (AC-24). Binary values are
    /// hex in groups of four bytes separated by spaces so the scanner's 16-digit rule cannot trip on a hash that happens to
    /// contain a long run of 0-9 hex digits.
    /// </summary>
    /// <returns>The dump.</returns>
    public async Task<string> DumpAllRowsAsync()
    {
        var dump = new StringBuilder();
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        var tables = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
            await using var tableReader = await list.ExecuteReaderAsync();
            while (await tableReader.ReadAsync())
            {
                tables.Add(tableReader.GetString(0));
            }
        }

        foreach (var table in tables)
        {
            await using var select = connection.CreateCommand();
            select.CommandText = $"SELECT * FROM {string.Join('.', table.Split('.').Select(p => $"[{p}]"))}";
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dump.Append(table).Append(':');
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    var text = value switch
                    {
                        DBNull => "NULL",
                        byte[] bytes => string.Join(' ', bytes.Chunk(4).Select(Convert.ToHexString)),
                        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    };
                    dump.Append(' ').Append(reader.GetName(i)).Append('=').Append(text);
                }

                dump.AppendLine();
            }
        }

        return dump.ToString();
    }
}
