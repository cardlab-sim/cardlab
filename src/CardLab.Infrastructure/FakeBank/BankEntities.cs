namespace CardLab.Infrastructure.FakeBank;

/// <summary>Row of <c>BankAccount</c> (fake bank). Never joined with card or payment tables (DEC-2).</summary>
public sealed class BankAccountRow
{
    /// <summary>PK (= the linked account's bank reference).</summary>
    public Guid Id { get; set; }

    /// <summary><c>char(4)</c>.</summary>
    public string LastFour { get; set; } = "";

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal AvailableBalance { get; set; }
}

/// <summary>Row of <c>BankDebitRequest</c>: the bank's memory of every debit request it received.</summary>
public sealed class BankDebitRequestRow
{
    /// <summary>PK, <c>varchar(12)</c>.</summary>
    public string Reference { get; set; } = "";

    /// <summary>FK BankAccount.</summary>
    public Guid AccountId { get; set; }

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>varchar(30)</c>: Completed, RefusedInsufficientFunds or RefusedAccountUnavailable.</summary>
    public string Outcome { get; set; } = "";

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary><c>datetime2(3) null</c>.</summary>
    public DateTime? DebitedAtUtc { get; set; }

    /// <summary>The outcome came from steering.</summary>
    public bool Simulated { get; set; }
}

/// <summary>Row of <c>BankEntry</c>: the ledger.</summary>
public sealed class BankEntryRow
{
    /// <summary>PK.</summary>
    public Guid Id { get; set; }

    /// <summary>FK BankAccount.</summary>
    public Guid AccountId { get; set; }

    /// <summary><c>varchar(12)</c>, unique.</summary>
    public string Reference { get; set; } = "";

    /// <summary><c>decimal(12,2)</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string Narrative { get; set; } = "";

    /// <summary><c>datetime2(3)</c>, UTC.</summary>
    public DateTime PostedAtUtc { get; set; }
}

/// <summary>Row of <c>BankSteering</c>: the single one-shot steering row (Id 1).</summary>
public sealed class BankSteeringRow
{
    /// <summary>PK, always 1.</summary>
    public int Id { get; set; }

    /// <summary><c>varchar(30) null</c>: <see cref="NextDebitOutcome"/> name.</summary>
    public string? NextDebitOutcome { get; set; }

    /// <summary><c>varchar(20) null</c>: <see cref="NextStatusCheck"/> name.</summary>
    public string? NextStatusCheck { get; set; }
}
