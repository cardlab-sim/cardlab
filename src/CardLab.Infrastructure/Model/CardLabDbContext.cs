using Microsoft.EntityFrameworkCore;

namespace CardLab.Infrastructure.Model;

/// <summary>EF Core model of the CardLab database (design §6); migrations in <c>Migrations/</c> (DEC-9).</summary>
/// <remarks>
/// <c>OnModelCreating</c> maps the tables, column types, keys, unique indexes and
/// <c>IX_Transaction_Spend (CardId, Category, Outcome, OccurredAt) INCLUDE (Amount)</c> plus the history
/// indexes <c>(CardId, DecidedAtUtc DESC)</c> and <c>(CardId, AtUtc DESC)</c>. There is no card number column (NFR-1).
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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessAccountRow>(e =>
        {
            e.ToTable("BusinessAccount");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(64).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AdminUserRow>(e =>
        {
            e.ToTable("AdminUser");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.HasOne<BusinessAccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AccountId, x.DisplayName }).IsUnique();
        });

        modelBuilder.Entity<CardRow>(e =>
        {
            e.ToTable("Card");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.CardholderName).HasMaxLength(200).IsRequired();
            e.Property(x => x.LastFour).HasMaxLength(4).IsFixedLength().IsUnicode(false).IsRequired();
            e.Property(x => x.LookupHash).HasMaxLength(32).IsFixedLength().IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasOne<BusinessAccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.LookupHash).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<SpendLimitRow>(e =>
        {
            e.ToTable("SpendLimit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Category).HasMaxLength(40).IsUnicode(false).IsRequired();
            e.Property(x => x.Period).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne<CardRow>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CardId, x.Category, x.Period }).IsUnique();
        });

        modelBuilder.Entity<TransactionRow>(e =>
        {
            e.ToTable("Transaction");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Category).HasMaxLength(40).IsUnicode(false).IsRequired();
            e.Property(x => x.Merchant).HasMaxLength(200).IsRequired();
            e.Property(x => x.OccurredAt).HasColumnType("datetimeoffset(0)");
            e.Property(x => x.DecidedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.Outcome).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasOne<CardRow>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CardId, x.Category, x.Outcome, x.OccurredAt }, "IX_Transaction_Spend")
                .IncludeProperties(x => x.Amount);
            e.HasIndex(x => new { x.CardId, x.DecidedAtUtc }).IsDescending(false, true);
        });

        modelBuilder.Entity<AuditEventRow>(e =>
        {
            e.ToTable("AuditEvent");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
            e.Property(x => x.AtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.Action).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.OldValue).HasMaxLength(400);
            e.Property(x => x.NewValue).HasMaxLength(400);
            e.HasOne<CardRow>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CardId, x.AtUtc }).IsDescending(false, true);
        });
    }
}
