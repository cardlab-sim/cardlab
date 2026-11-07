namespace CardLab.Domain.Periods;

/// <summary>Calendar period over which spend in a category accumulates (R-4), in Europe/London.</summary>
public enum Period
{
    /// <summary>Calendar day, local midnight to midnight.</summary>
    Day,

    /// <summary>Calendar week, Monday 00:00 to the next Monday 00:00.</summary>
    Week,

    /// <summary>Calendar month, the 1st 00:00 to the next 1st 00:00.</summary>
    Month,
}
