namespace CardLab.Infrastructure.Model;

/// <summary>Row of <c>LinkedAccount</c> (design §6): one per business. Holds only the last four of the account number (NFR-3).</summary>
public sealed class LinkedAccountRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK BusinessAccount; unique.</summary>
    public Guid AccountId { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary><c>char(4)</c>.</summary>
    public string LastFour { get; set; } = "";

    /// <summary>The account's id at the bank (no foreign key: the fake bank is a separate world, DEC-2).</summary>
    public Guid BankAccountRef { get; set; }

    /// <summary><c>nvarchar(400)</c>.</summary>
    public string AuthorityBasis { get; set; } = "";
}

/// <summary>Row of <c>PaymentQuote</c>. Only the status columns ever change (R-14).</summary>
public sealed class PaymentQuoteRow
{
    /// <summary>PK, <c>varchar(12)</c>.</summary>
    public string Reference { get; set; } = "";

    /// <summary>FK Card.</summary>
    public Guid CardId { get; set; }

    /// <summary>FK LinkedAccount.</summary>
    public Guid LinkedAccountId { get; set; }

    /// <summary><c>varchar(10)</c>: enum name.</summary>
    public string Kind { get; set; } = "";

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Fee { get; set; }

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Total { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string FromLabel { get; set; } = "";

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string ToLabel { get; set; } = "";

    /// <summary><c>varchar(10)</c>.</summary>
    public string WordingVersion { get; set; } = "";

    /// <summary><c>nvarchar(100)</c>.</summary>
    public string Heading { get; set; } = "";

    /// <summary><c>nvarchar(2000)</c>: the wording lines joined with a line feed.</summary>
    public string RenderedLines { get; set; } = "";

    /// <summary><c>nvarchar(100)</c>.</summary>
    public string ButtonLabel { get; set; } = "";

    /// <summary><c>varchar(12)</c>: enum name.</summary>
    public string ActorKind { get; set; } = "";

    /// <summary>Actor id.</summary>
    public Guid ActorId { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string ActorName { get; set; } = "";

    /// <summary><c>nvarchar(400)</c>.</summary>
    public string AuthorityBasis { get; set; } = "";

    /// <summary><c>varchar(20)</c>: enum name.</summary>
    public string Origin { get; set; } = "";

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary><c>varchar(10)</c>: Open, Confirmed or Refused.</summary>
    public string Status { get; set; } = "";

    /// <summary><c>datetime2(3) null</c>.</summary>
    public DateTime? RefusedAtUtc { get; set; }

    /// <summary><c>varchar(30) null</c>.</summary>
    public string? RefusalReason { get; set; }

    /// <summary><c>decimal(12,2) null</c>.</summary>
    public decimal? BalanceAtRefusal { get; set; }
}

/// <summary>Row of <c>Payment</c>. Unique per reference; at most one Pending per card (filtered unique index).</summary>
public sealed class PaymentRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK PaymentQuote; unique.</summary>
    public string Reference { get; set; } = "";

    /// <summary>FK Card.</summary>
    public Guid CardId { get; set; }

    /// <summary><c>varchar(10)</c>: enum name.</summary>
    public string Kind { get; set; } = "";

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Fee { get; set; }

    /// <summary><c>varchar(10)</c>: Pending, Completed or Failed.</summary>
    public string State { get; set; } = "";

    /// <summary><c>datetime2(3)</c>: acceptance time, UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary><c>datetime2(3) null</c>.</summary>
    public DateTime? SettledAtUtc { get; set; }

    /// <summary><c>datetime2(3) null</c>.</summary>
    public DateTime? EffectiveAtUtc { get; set; }

    /// <summary><c>datetime2(3) null</c>.</summary>
    public DateTime? BankDebitedAtUtc { get; set; }

    /// <summary><c>varchar(30) null</c>: enum name.</summary>
    public string? FailureReason { get; set; }

    /// <summary><c>decimal(12,2) null</c>.</summary>
    public decimal? BalanceBefore { get; set; }

    /// <summary><c>decimal(12,2) null</c>.</summary>
    public decimal? BalanceAfter { get; set; }

    /// <summary>The outcome came from the fake bank's steering.</summary>
    public bool Simulated { get; set; }
}

/// <summary>Row of <c>PaymentEvent</c>. Append-only.</summary>
public sealed class PaymentEventRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK PaymentQuote.</summary>
    public string Reference { get; set; } = "";

    /// <summary>FK Payment, when one exists.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary><c>varchar(30)</c>: enum name.</summary>
    public string Kind { get; set; } = "";

    /// <summary><c>varchar(10) null</c>.</summary>
    public string? FromState { get; set; }

    /// <summary><c>varchar(10) null</c>.</summary>
    public string? ToState { get; set; }

    /// <summary><c>nvarchar(400) null</c>.</summary>
    public string? Detail { get; set; }

    /// <summary>The bank outcome came from steering.</summary>
    public bool Simulated { get; set; }
}
