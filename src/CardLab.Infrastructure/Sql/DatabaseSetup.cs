using CardLab.Infrastructure.Options;

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
    /// <remarks>Implemented in PR-06.</remarks>
    public static Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default) => throw new NotImplementedException("PR-06");
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
    /// <remarks>Implemented in PR-06.</remarks>
    public static Task EnsureSeedAsync(IServiceProvider services, SeedOptions options, CancellationToken ct = default) => throw new NotImplementedException("PR-06");
}
