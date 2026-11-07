using CardLab.Infrastructure.FakeBank;
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

    /// <summary>LinkedAccount table (002).</summary>
    public DbSet<LinkedAccountRow> LinkedAccounts => Set<LinkedAccountRow>();

    /// <summary>PaymentQuote table (002).</summary>
    public DbSet<PaymentQuoteRow> PaymentQuotes => Set<PaymentQuoteRow>();

    /// <summary>Payment table (002).</summary>
    public DbSet<PaymentRow> Payments => Set<PaymentRow>();

    /// <summary>PaymentEvent table (002, append-only).</summary>
    public DbSet<PaymentEventRow> PaymentEvents => Set<PaymentEventRow>();

    /// <summary>BankAccount table (fake bank).</summary>
    public DbSet<BankAccountRow> BankAccounts => Set<BankAccountRow>();

    /// <summary>BankDebitRequest table (fake bank).</summary>
    public DbSet<BankDebitRequestRow> BankDebitRequests => Set<BankDebitRequestRow>();

    /// <summary>BankEntry table (fake bank ledger).</summary>
    public DbSet<BankEntryRow> BankEntries => Set<BankEntryRow>();

    /// <summary>BankSteering table (fake bank, one row).</summary>
    public DbSet<BankSteeringRow> BankSteerings => Set<BankSteeringRow>();

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

        modelBuilder.Entity<LinkedAccountRow>(e =>
        {
            e.ToTable("LinkedAccount");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.LastFour).HasMaxLength(4).IsFixedLength().IsUnicode(false).IsRequired();
            e.Property(x => x.AuthorityBasis).HasMaxLength(400).IsRequired();
            e.HasOne<BusinessAccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AccountId).IsUnique();
        });

        modelBuilder.Entity<PaymentQuoteRow>(e =>
        {
            e.ToTable("PaymentQuote");
            e.HasKey(x => x.Reference);
            e.Property(x => x.Reference).HasMaxLength(12).IsUnicode(false).ValueGeneratedNever();
            e.Property(x => x.Kind).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Fee).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.FromLabel).HasMaxLength(200).IsRequired();
            e.Property(x => x.ToLabel).HasMaxLength(200).IsRequired();
            e.Property(x => x.WordingVersion).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.Heading).HasMaxLength(100).IsRequired();
            e.Property(x => x.RenderedLines).HasMaxLength(2000).IsRequired();
            e.Property(x => x.ButtonLabel).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorKind).HasMaxLength(12).IsUnicode(false).IsRequired();
            e.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
            e.Property(x => x.AuthorityBasis).HasMaxLength(400).IsRequired();
            e.Property(x => x.Origin).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.Status).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.RefusedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.RefusalReason).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.BalanceAtRefusal).HasPrecision(12, 2);
            e.HasOne<CardRow>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LinkedAccountRow>().WithMany().HasForeignKey(x => x.LinkedAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CardId, x.CreatedAtUtc }).IsDescending(false, true);
        });

        modelBuilder.Entity<PaymentRow>(e =>
        {
            e.ToTable("Payment");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reference).HasMaxLength(12).IsUnicode(false).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Fee).HasPrecision(12, 2);
            e.Property(x => x.State).HasMaxLength(10).IsUnicode(false).IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.SettledAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.EffectiveAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.BankDebitedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.FailureReason).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.BalanceBefore).HasPrecision(12, 2);
            e.Property(x => x.BalanceAfter).HasPrecision(12, 2);
            e.HasOne<PaymentQuoteRow>().WithMany().HasForeignKey(x => x.Reference).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CardRow>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.CardId, "UX_Payment_OnePending").IsUnique().HasFilter("[State] = 'Pending'");
            e.HasIndex(x => new { x.CardId, x.CreatedAtUtc }).IsDescending(false, true);
            e.HasIndex(x => new { x.CardId, x.State }, "IX_Payment_Completed").IncludeProperties(x => x.Amount);
        });

        modelBuilder.Entity<PaymentEventRow>(e =>
        {
            e.ToTable("PaymentEvent");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reference).HasMaxLength(12).IsUnicode(false).IsRequired();
            e.Property(x => x.AtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.Kind).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(x => x.FromState).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.ToState).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Detail).HasMaxLength(400);
            e.HasOne<PaymentQuoteRow>().WithMany().HasForeignKey(x => x.Reference).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentRow>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Reference, x.AtUtc });
        });

        modelBuilder.Entity<BankAccountRow>(e =>
        {
            e.ToTable("BankAccount");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.LastFour).HasMaxLength(4).IsFixedLength().IsUnicode(false).IsRequired();
            e.Property(x => x.AvailableBalance).HasPrecision(12, 2);
        });

        modelBuilder.Entity<BankDebitRequestRow>(e =>
        {
            e.ToTable("BankDebitRequest");
            e.HasKey(x => x.Reference);
            e.Property(x => x.Reference).HasMaxLength(12).IsUnicode(false).ValueGeneratedNever();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Outcome).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2(3)");
            e.Property(x => x.DebitedAtUtc).HasColumnType("datetime2(3)");
            e.HasOne<BankAccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankEntryRow>(e =>
        {
            e.ToTable("BankEntry");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reference).HasMaxLength(12).IsUnicode(false).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Narrative).HasMaxLength(200).IsRequired();
            e.Property(x => x.PostedAtUtc).HasColumnType("datetime2(3)");
            e.HasOne<BankAccountRow>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.PostedAtUtc }).IsDescending(false, true);
        });

        modelBuilder.Entity<BankSteeringRow>(e =>
        {
            e.ToTable("BankSteering");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NextDebitOutcome).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.NextStatusCheck).HasMaxLength(20).IsUnicode(false);
        });
    }
}
