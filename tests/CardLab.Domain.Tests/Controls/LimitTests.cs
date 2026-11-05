using CardLab.Domain.Controls;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Controls;

public sealed class LimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    // T-U-4 · PR-05 · unit
    // Given no Fuel/Day limit, When SetLimit(null, Fuel/Day £50.00), Then the limit and a LimitSet event with old null, new "Fuel/Day/50.00"
    // Catches: LimitChanged emitted for a first set (AC-6)
    [Fact]
    public void Setting_a_new_limit_records_limit_set()
    {
        var card = new CardBuilder().BuildRef();
        var requested = new LimitBuilder().Of("50.00").Build();

        var change = CardControls.SetLimit(null, requested, card, ActorBuilder.Sam, Now);

        Assert.Equal(requested, change.NewState);
        Assert.Equal(new AuditEvent(card, ActorBuilder.Sam, Now, AuditAction.LimitSet, null, "Fuel/Day/50.00"), change.Event);
    }

    // T-U-5 · PR-05 · unit
    // Given Fuel/Day £50.00, When SetLimit(existing, Fuel/Day £80.00), Then the £80.00 limit and LimitChanged old "Fuel/Day/50.00", new "Fuel/Day/80.00"
    // Catches: old value lost on replace (AC-7)
    [Fact]
    public void Replacing_a_limit_records_limit_changed_with_old_and_new()
    {
        var card = new CardBuilder().BuildRef();
        var existing = new LimitBuilder().Of("50.00").Build();
        var requested = new LimitBuilder().Of("80.00").Build();

        var change = CardControls.SetLimit(existing, requested, card, ActorBuilder.Sam, Now);

        Assert.Equal(requested, change.NewState);
        Assert.Equal(new AuditEvent(card, ActorBuilder.Sam, Now, AuditAction.LimitChanged, "Fuel/Day/50.00", "Fuel/Day/80.00"), change.Event);
    }

    // T-U-6 · PR-05 · unit
    // Given Fuel/Day £80.00, When RemoveLimit(existing), Then LimitRemoved old "Fuel/Day/80.00", new null; and RemoveLimit(null) → null
    // Catches: audit on removing a non-existent limit; missing old value (AC-8)
    [Fact]
    public void Removing_a_limit_records_limit_removed_and_absent_limit_is_a_no_op()
    {
        var card = new CardBuilder().BuildRef();
        var existing = new LimitBuilder().Of("80.00").Build();

        var change = CardControls.RemoveLimit(existing, card, ActorBuilder.Sam, Now);

        Assert.NotNull(change);
        Assert.Equal(existing, change.NewState);
        Assert.Equal(new AuditEvent(card, ActorBuilder.Sam, Now, AuditAction.LimitRemoved, "Fuel/Day/80.00", null), change.Event);
        Assert.Null(CardControls.RemoveLimit(null, card, ActorBuilder.Sam, Now));
    }
}
