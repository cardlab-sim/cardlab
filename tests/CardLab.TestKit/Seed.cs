namespace CardLab.TestKit;

/// <summary>
/// Seed constants shared by every test layer; must match <c>src/CardLab.Api/appsettings.json</c> (<c>CardLab:Seed</c>)
/// and <c>tests/e2e/src/seed.ts</c>. Fictional data. Complete in PR-00.
/// </summary>
public static class Seed
{
    /// <summary>Business account name.</summary>
    public const string AccountName = "Brightline Supplies Ltd";

    /// <summary>Account time zone.</summary>
    public const string TimeZoneId = "Europe/London";

    /// <summary>Seeded admin display name.</summary>
    public const string AdminName = "Sam Ellis";

    /// <summary>Seeded admin id.</summary>
    public static readonly Guid AdminId = Guid.Parse("5a3e1d2c-7b4f-4c1a-9e2d-000000000001");

    /// <summary>Development lookup key (non-secret; same as appsettings.Development.json).</summary>
    public const string DevLookupKey = "Df0oEDVTMdwx6fvPtK0ZQMvna85qSD/VCAc60X2mK/0=";

    /// <summary>"Ops van".</summary>
    public static readonly SeedCard OpsVan = new("Ops van", "Priya Nair", "9999000100000001", "0001");

    /// <summary>"Field sales".</summary>
    public static readonly SeedCard FieldSales = new("Field sales", "Tom Okafor", "9999000100000002", "0002");

    /// <summary>"Marketing".</summary>
    public static readonly SeedCard Marketing = new("Marketing", "Leah Brooks", "9999000100000003", "0003");

    /// <summary>All seeded cards.</summary>
    public static readonly IReadOnlyList<SeedCard> Cards = [OpsVan, FieldSales, Marketing];

    /// <summary>A 16-digit number that belongs to no card (AC-19).</summary>
    public const string UnknownCardNumber = "9999000199999999";

    /// <summary>002: linked current account display name.</summary>
    public const string LinkedAccountName = "Brightline Supplies Ltd current account";

    /// <summary>002: fictional full account number (appsettings only; must never appear in a row, response, log or page, NFR-3).</summary>
    public const string LinkedAccountNumber = "51907731";

    /// <summary>002: last four of the linked account.</summary>
    public const string LinkedAccountLastFour = "7731";

    /// <summary>002: fake bank opening available balance (decimal text).</summary>
    public const string BankOpeningBalance = "25000.00";

    /// <summary>002: standing authority text (R-2).</summary>
    public const string AuthorityBasis = "Standing authority of Brightline Supplies Ltd for the named cardholder to repay their own card from this account";

    /// <summary>002: bank-side id of the linked account (= fake bank <c>BankAccount.Id</c>).</summary>
    public static readonly Guid BankAccountRef = Guid.Parse("5a3e1d2c-7b4f-4c1a-9e2d-000000000731");
}

/// <summary>One seeded card.</summary>
/// <param name="Name">Card name.</param>
/// <param name="CardholderName">Cardholder.</param>
/// <param name="CardNumber">Fictional 16-digit number.</param>
/// <param name="LastFour">Last four digits.</param>
public sealed record SeedCard(string Name, string CardholderName, string CardNumber, string LastFour);
