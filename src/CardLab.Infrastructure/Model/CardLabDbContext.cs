using Microsoft.EntityFrameworkCore;

namespace CardLab.Infrastructure.Model;

/// <summary>EF Core model of the CardLab database (design §6); migrations in <c>Migrations/</c> (DEC-9).</summary>
/// <remarks>
/// <c>OnModelCreating</c> maps the tables, column types, keys, unique indexes and
/// <c>IX_Transaction_Spend (CardId, Category, Outcome, OccurredAt) INCLUDE (Amount)</c> plus the history
/// indexes <c>(CardId, DecidedAtUtc DESC)</c> and <c>(CardId, AtUtc DESC)</c>. Implemented in PR-06.
/// </remarks>
/// <param name="options">Configured by <c>AddCardLabInfrastructure</c>.</param>
public sealed class CardLabDbContext(DbContextOptions<CardLabDbContext> options) : DbContext(options)
{
    /// <summary>BusinessAccount table.</summary>
    public DbSet<BusinessAccountRow> BusinessAccounts => Set<BusinessAccountRow>();

    /// <summary>AdminUser table.</summary>
    public DbSet<AdminUserRow> AdminUsers => Set<AdminUserRow>();

    /// <summary>Card table.</summary>
    public DbSet<CardRow> Cards => Set<CardRow>();

    /// <summary>SpendLimit table.</summary>
    public DbSet<SpendLimitRow> SpendLimits => Set<SpendLimitRow>();

    /// <summary>Transaction table (append-only).</summary>
    public DbSet<TransactionRow> Transactions => Set<TransactionRow>();

    /// <summary>AuditEvent table (append-only).</summary>
    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException("PR-06");
}
