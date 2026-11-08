namespace CardLab.Infrastructure.Options;

/// <summary>Host settings under the <c>CardLab</c> configuration section (design §11). Complete in PR-00.</summary>
public sealed class CardLabOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "CardLab";

    /// <summary>Maps <c>POST /api/test/reset</c> when true (dev + CI e2e only; never on <c>demo</c>, DEC-16).</summary>
    public bool TestEndpoints { get; set; }

    /// <summary>Build commit reported by <c>GET /api/health</c>; empty → assembly informational version.</summary>
    public string Commit { get; set; } = "";
}

/// <summary>The card-number lookup key (<c>CardLab:LookupKey</c>, DEC-8). Complete in PR-00.</summary>
public sealed class LookupOptions
{
    /// <summary>Configuration section the key is read from.</summary>
    public const string Section = "CardLab";

    /// <summary>Base64 of exactly 32 bytes. Development key committed (non-secret, fictional data); <c>demo</c> key set by the human.</summary>
    public string LookupKey { get; set; } = "";
}

/// <summary>Fictional seed data (<c>CardLab:Seed</c>, design §6). Non-secret. Complete in PR-00.</summary>
public sealed class SeedOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "CardLab:Seed";

    /// <summary>Business account name ("Brightline Supplies Ltd").</summary>
    public string AccountName { get; set; } = "";

    /// <summary>Account time zone (Europe/London).</summary>
    public string TimeZoneId { get; set; } = "";

    /// <summary>Fixed id of the seeded admin (PLANNER-DECISION: deterministic so <c>SeededAdminActor</c> needs no query).</summary>
    public Guid AdminId { get; set; }

    /// <summary>Admin display name ("Sam Ellis").</summary>
    public string AdminName { get; set; } = "";

    /// <summary>Seeded cards; matched by name on re-seed.</summary>
    public List<SeedCardOptions> Cards { get; set; } = [];

    /// <summary>The business's linked current account and its fake-bank account (002 design §6, §11).</summary>
    public SeedLinkedAccountOptions LinkedAccount { get; set; } = new();
}

/// <summary>
/// Seeded linked current account (<c>CardLab:Seed:LinkedAccount</c>, 002 design §6). Fictional, non-secret. Only the last
/// four digits of <see cref="AccountNumber"/> ever reach the database (NFR-3). Complete in 002 PR-00.
/// </summary>
public sealed class SeedLinkedAccountOptions
{
    /// <summary>"Brightline Supplies Ltd current account".</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Fictional full account number (digits); used only to derive the last four.</summary>
    public string AccountNumber { get; set; } = "";

    /// <summary>Fixed bank-side id of the account (also the fake bank's <c>BankAccount.Id</c>).</summary>
    public Guid BankAccountRef { get; set; }

    /// <summary>Opening available balance of the fake bank account, decimal text ("25000.00").</summary>
    public string OpeningBalance { get; set; } = "";

    /// <summary>The standing authority text named on every payment record (R-2).</summary>
    public string AuthorityBasis { get; set; } = "";
}

/// <summary>One seeded card. The number is used only to compute the lookup hash and last four; never stored.</summary>
public sealed class SeedCardOptions
{
    /// <summary>Card name, e.g. "Ops van".</summary>
    public string Name { get; set; } = "";

    /// <summary>Cardholder, e.g. "Priya Nair".</summary>
    public string CardholderName { get; set; } = "";

    /// <summary>Fictional 16-digit number, digits only.</summary>
    public string CardNumber { get; set; } = "";
}
