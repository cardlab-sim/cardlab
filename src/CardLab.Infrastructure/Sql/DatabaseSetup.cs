using CardLab.Domain.Controls;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Sql;

/// <summary>Startup schema migration and seeding (DEC-9).</summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Applies pending EF Core migrations (<c>MigrateAsync</c>, forward-only) and then
    /// <see cref="Seeder.EnsureSeedAsync"/> with the bound <see cref="SeedOptions"/>. Called once by the host before it
    /// starts serving; a failure stops the host (design §9).
    /// </summary>
    /// <param name="services">Root service provider (a scope is created inside).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
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
    /// existing cards keep their status, limits and history.
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

        await db.SaveChangesAsync(ct);
    }
}
