namespace CardLab.Domain.Tests.Controls;

public sealed class FreezeTests
{
    // T-U-1 · PR-05 · unit
    // Given an Active card ref (last four 0001) and Sam at 2026-11-04T10:00Z, When CardControls.Freeze, Then NewState Frozen and one event: Freeze, actor Sam, AtUtc as given, card last four 0001, old "Active", new "Frozen"
    // Catches: missing audit fields (AC-1)
    [Fact(Skip = "todo")]
    public void Freezing_an_active_card_produces_frozen_and_an_audit_event()
    {
    }

    // T-U-2 · PR-05 · unit
    // Given a Frozen card, When Freeze, Then null (no change, no event)
    // Catches: duplicate audit events on repeat freeze (AC-2)
    [Fact(Skip = "todo")]
    public void Freezing_a_frozen_card_is_a_no_op()
    {
    }

    // T-U-3 · PR-05 · unit
    // Given a Frozen card, When Unfreeze, Then Active and one Unfreeze event, old "Frozen", new "Active"
    // Catches: swapped old/new values (AC-3)
    [Fact(Skip = "todo")]
    public void Unfreezing_a_frozen_card_produces_active_and_an_audit_event()
    {
    }

    // T-U-20 · PR-05 · unit
    // Given an Active card, When Unfreeze, Then null
    // Catches: audit noise on unfreezing an active card (AC-40)
    [Fact(Skip = "todo")]
    public void Unfreezing_an_active_card_is_a_no_op()
    {
    }
}
