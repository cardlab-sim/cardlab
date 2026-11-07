using System.Diagnostics;
using System.Globalization;
using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.FakeBank;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Sql;

/// <summary>Startup schema migration and seeding (DEC-9).</summary>
public static class DatabaseSetup
{
    private static readonly TimeSpan RetryWindow = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private static readonly Action<ILogger, Exception?> LogAttemptFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, "DatabaseNotReady"),
        "Database not ready during startup; retrying.");

    /// <summary>
    /// Applies pending EF Core migrations (<c>MigrateAsync</c>, forward-only) and then
    /// <see cref="Seeder.EnsureSeedAsync"/> with the bound <see cref="SeedOptions"/>. Called once by the host before it
    /// starts serving. A <see cref="SqlException"/> (for example a serverless database resuming from idle auto-pause,
    /// BUG-008) is retried every 5 s for up to 120 s, logging one fixed message per attempt without connection details;
    /// after the deadline the last exception is rethrown and stops the host (design §9).
    /// </summary>
    /// <param name="services">Root service provider (a scope is created inside).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default) =>
        InitialiseDatabaseAsync(services, RetryWindow, RetryDelay, ct);

    /// <summary>The retry loop of <see cref="InitialiseDatabaseAsync(IServiceProvider, CancellationToken)"/> with injectable timing (tests).</summary>
    /// <param name="services">Root service provider.</param>
    /// <param name="retryWindow">Total time during which a <see cref="SqlException"/> is retried.</param>
    /// <param name="retryDelay">Delay between attempts.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    internal static async Task InitialiseDatabaseAsync(IServiceProvider services, TimeSpan retryWindow, TimeSpan retryDelay, CancellationToken ct)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseSetup).FullName!) ?? NullLogger.Instance;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                await MigrateAndSeedAsync(services, ct);
                return;
            }
            catch (SqlException) when (!ct.IsCancellationRequested && clock.Elapsed + retryDelay < retryWindow)
            {
                LogAttemptFailed(logger, null);
                await Task.Delay(retryDelay, ct);
            }
        }
    }

    private static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CardLabDbContext>();
        await db.Database.MigrateAsync(ct);
        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        await Seeder.EnsureSeedAsync(scope.ServiceProvider, seed, ct);
    }
}

/// <summary>Idempotent seed of the fictional account, admin and cards (design §6, DEC-9).</summary>
public static class Seeder
{
    /// <summary>
    /// Ensures the account (by name), the admin (by <see cref="SeedOptions.AdminId"/>) and each card (by name)
    /// exist; never duplicates; recomputes every seeded card's lookup hash and last four from its number with
    /// the configured <c>ICardNumberLookup</c> (heals a key rotation). New cards start Active with no limits;
    /// existing cards keep their status, limits and history. Also seeds (002, only when missing) the linked current account,
    /// the fake bank account at its opening balance and the steering row.
    /// </summary>
    /// <param name="services">A scoped service provider (DbContext + lookup).</param>
    /// <param name="options">Seed data.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    public static async Task EnsureSeedAsync(IServiceProvider services, SeedOptions options, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<CardLabDbContext>();
        var lookup = services.GetRequiredService<ICardNumberLookup>();

        var account = await db.BusinessAccounts.SingleOrDefaultAsync(a => a.Name == options.AccountName, ct);
        if (account is null)
        {
            account = new BusinessAccountRow { Id = Guid.NewGuid(), Name = options.AccountName, TimeZoneId = options.TimeZoneId };
            db.BusinessAccounts.Add(account);
        }

        if (!await db.AdminUsers.AnyAsync(a => a.Id == options.AdminId, ct))
        {
            db.AdminUsers.Add(new AdminUserRow { Id = options.AdminId, AccountId = account.Id, DisplayName = options.AdminName });
        }

        foreach (var seedCard in options.Cards)
        {
            var hash = lookup.Compute(seedCard.CardNumber).ToBytes();
            var lastFour = seedCard.CardNumber[^4..];
            var card = await db.Cards.SingleOrDefaultAsync(c => c.AccountId == account.Id && c.Name == seedCard.Name, ct);
            if (card is null)
            {
                db.Cards.Add(new CardRow
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Name = seedCard.Name,
                    CardholderName = seedCard.CardholderName,
                    LastFour = lastFour,
                    LookupHash = hash,
                    Status = CardStatus.Active.ToString(),
                });
            }
            else
            {
                card.LookupHash = hash;
                card.LastFour = lastFour;
            }
        }

        await SeedLinkedAccountAsync(db, account.Id, options.LinkedAccount, ct);
        await db.SaveChangesAsync(ct);
    }

    // 002: the linked account, its fake bank account and the steering row, each only when missing (a restart never
    // resets the bank, AC-22). Only the last four of the account number is kept (NFR-3). Skipped when no account number is
    // configured, so 001-era seed options keep working.
    private static async Task SeedLinkedAccountAsync(CardLabDbContext db, Guid accountId, SeedLinkedAccountOptions linked, CancellationToken ct)
    {
        if (linked.AccountNumber.Length < 4)
        {
            return; // No linked account configured (pre-002 seed options): nothing to seed.
        }

        var lastFour = linked.AccountNumber[^4..];
        if (!await db.LinkedAccounts.AnyAsync(l => l.AccountId == accountId, ct))
        {
            db.LinkedAccounts.Add(new LinkedAccountRow
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                DisplayName = linked.DisplayName,
                LastFour = lastFour,
                BankAccountRef = linked.BankAccountRef,
                AuthorityBasis = linked.AuthorityBasis,
            });
        }

        if (!await db.BankAccounts.AnyAsync(b => b.Id == linked.BankAccountRef, ct))
        {
            db.BankAccounts.Add(new BankAccountRow
            {
                Id = linked.BankAccountRef,
                LastFour = lastFour,
                AvailableBalance = decimal.Parse(linked.OpeningBalance, NumberStyles.Number, CultureInfo.InvariantCulture),
            });
        }

        if (!await db.BankSteerings.AnyAsync(s => s.Id == 1, ct))
        {
            db.BankSteerings.Add(new BankSteeringRow { Id = 1 });
        }
    }
}
