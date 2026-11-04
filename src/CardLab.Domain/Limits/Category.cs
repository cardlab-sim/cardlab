namespace CardLab.Domain.Limits;

/// <summary>The fixed spend categories (R-4). The request names the category; no MCC mapping.</summary>
public enum Category
{
    /// <summary>Travel.</summary>
    Travel,

    /// <summary>Fuel.</summary>
    Fuel,

    /// <summary>Meals &amp; Entertainment.</summary>
    MealsAndEntertainment,

    /// <summary>Software &amp; Subscriptions.</summary>
    SoftwareAndSubscriptions,

    /// <summary>Office Supplies.</summary>
    OfficeSupplies,

    /// <summary>Advertising.</summary>
    Advertising,

    /// <summary>Professional Services.</summary>
    ProfessionalServices,

    /// <summary>Other.</summary>
    Other,
}
