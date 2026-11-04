namespace CardLab.Domain.Tests.Controls;

public sealed class LimitTests
{
    // T-U-4 · PR-05 · unit
    // Given no Fuel/Day limit, When SetLimit(null, Fuel/Day £50.00), Then the limit and a LimitSet event with old null, new "Fuel/Day/50.00"
    // Catches: LimitChanged emitted for a first set (AC-6)
    [Fact(Skip = "todo")]
    public void Setting_a_new_limit_records_limit_set()
    {
    }

    // T-U-5 · PR-05 · unit
    // Given Fuel/Day £50.00, When SetLimit(existing, Fuel/Day £80.00), Then the £80.00 limit and LimitChanged old "Fuel/Day/50.00", new "Fuel/Day/80.00"
    // Catches: old value lost on replace (AC-7)
    [Fact(Skip = "todo")]
    public void Replacing_a_limit_records_limit_changed_with_old_and_new()
    {
    }

    // T-U-6 · PR-05 · unit
    // Given Fuel/Day £80.00, When RemoveLimit(existing), Then LimitRemoved old "Fuel/Day/80.00", new null; and RemoveLimit(null) → null
    // Catches: audit on removing a non-existent limit; missing old value (AC-8)
    [Fact(Skip = "todo")]
    public void Removing_a_limit_records_limit_removed_and_absent_limit_is_a_no_op()
    {
    }
}
