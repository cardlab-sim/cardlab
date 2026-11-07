namespace CardLab.Infrastructure.Model;

/// <summary>Row of <c>BusinessAccount</c> (design §6).</summary>
public sealed class BusinessAccountRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary><c>nvarchar(200)</c>, unique.</summary>
    public string Name { get; set; } = "";

    /// <summary><c>varchar(64)</c>, "Europe/London".</summary>
    public string TimeZoneId { get; set; } = "";
}

/// <summary>Row of <c>AdminUser</c>.</summary>
public sealed class AdminUserRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK BusinessAccount.</summary>
    public Guid AccountId { get; set; }

    /// <summary><c>nvarchar(200)</c>; unique with AccountId.</summary>
    public string DisplayName { get; set; } = "";
}

/// <summary>Row of <c>Card</c>. Holds no card number (NFR-1): only the lookup hash and last four.</summary>
public sealed class CardRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK BusinessAccount.</summary>
    public Guid AccountId { get; set; }

    /// <summary><c>nvarchar(100)</c>; unique with AccountId.</summary>
    public string Name { get; set; } = "";

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string CardholderName { get; set; } = "";

    /// <summary><c>char(4)</c>.</summary>
    public string LastFour { get; set; } = "";

    /// <summary><c>binary(32)</c>, unique.</summary>
    public byte[] LookupHash { get; set; } = [];

    /// <summary><c>varchar(20)</c>: enum name "Active" / "Frozen".</summary>
    public string Status { get; set; } = "";
}

/// <summary>Row of <c>SpendLimit</c>; unique (CardId, Category, Period).</summary>
public sealed class SpendLimitRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK Card.</summary>
    public Guid CardId { get; set; }

    /// <summary><c>varchar(40)</c>: enum name.</summary>
    public string Category { get; set; } = "";

    /// <summary><c>varchar(10)</c>: enum name.</summary>
    public string Period { get; set; } = "";

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }
}

/// <summary>Row of <c>Transaction</c>. Append-only: no update or delete code paths.</summary>
public sealed class TransactionRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK Card.</summary>
    public Guid CardId { get; set; }

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>varchar(40)</c>: enum name.</summary>
    public string Category { get; set; } = "";

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string Merchant { get; set; } = "";

    /// <summary><c>datetimeoffset(0)</c>, as supplied.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime DecidedAtUtc { get; set; }

    /// <summary><c>varchar(10)</c>: enum name.</summary>
    public string Outcome { get; set; } = "";

    /// <summary><c>varchar(20)</c>: enum name.</summary>
    public string Reason { get; set; } = "";
}

/// <summary>Row of <c>AuditEvent</c>. Append-only.</summary>
public sealed class AuditEventRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK Card.</summary>
    public Guid CardId { get; set; }

    /// <summary>Admin id.</summary>
    public Guid ActorId { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string ActorName { get; set; } = "";

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary><c>varchar(20)</c>: enum name.</summary>
    public string Action { get; set; } = "";

    /// <summary><c>nvarchar(400) null</c>.</summary>
    public string? OldValue { get; set; }

    /// <summary><c>nvarchar(400) null</c>.</summary>
    public string? NewValue { get; set; }
}
