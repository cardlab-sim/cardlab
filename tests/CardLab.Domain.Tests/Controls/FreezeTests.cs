using CardLab.Domain.Controls;
using CardLab.TestKit.Builders;

namespace CardLab.Domain.Tests.Controls;

public sealed class FreezeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    // T-U-1 · PR-05 · unit
    // Given an Active card ref (last four 0001) and Sam at 2026-11-04T10:00Z, When CardControls.Freeze, Then NewState Frozen and one event: Freeze, actor Sam, AtUtc as given, card last four 0001, old "Active", new "Frozen"
    // Catches: missing audit fields (AC-1)
    [Fact]
    public void Freezing_an_active_card_produces_frozen_and_an_audit_event()
    {
        var card = new CardBuilder().WithCardNumber("4000000000000001").BuildRef();

        var change = CardControls.Freeze(CardStatus.Active, card, ActorBuilder.Sam, Now);

        Assert.NotNull(change);
        Assert.Equal(CardStatus.Frozen, change.NewState);
        Assert.Equal(new AuditEvent(card, ActorBuilder.Sam, Now, AuditAction.Freeze, "Active", "Frozen"), change.Event);
        Assert.Equal("0001", change.Event.Card.LastFour);
    }

    // T-U-2 · PR-05 · unit
    // Given a Frozen card, When Freeze, Then null (no change, no event)
    // Catches: duplicate audit events on repeat freeze (AC-2)
    [Fact]
    public void Freezing_a_frozen_card_is_a_no_op()
    {
        var change = CardControls.Freeze(CardStatus.Frozen, new CardBuilder().BuildRef(), ActorBuilder.Sam, Now);

        Assert.Null(change);
    }

    // T-U-3 · PR-05 · unit
    // Given a Frozen card, When Unfreeze, Then Active and one Unfreeze event, old "Frozen", new "Active"
    // Catches: swapped old/new values (AC-3)
    [Fact]
    public void Unfreezing_a_frozen_card_produces_active_and_an_audit_event()
    {
        var card = new CardBuilder().BuildRef();

        var change = CardControls.Unfreeze(CardStatus.Frozen, card, ActorBuilder.Sam, Now);

        Assert.NotNull(change);
        Assert.Equal(CardStatus.Active, change.NewState);
        Assert.Equal(new AuditEvent(card, ActorBuilder.Sam, Now, AuditAction.Unfreeze, "Frozen", "Active"), change.Event);
    }

    // T-U-20 · PR-05 · unit
    // Given an Active card, When Unfreeze, Then null
    // Catches: audit noise on unfreezing an active card (AC-40)
    [Fact]
    public void Unfreezing_an_active_card_is_a_no_op()
    {
        var change = CardControls.Unfreeze(CardStatus.Active, new CardBuilder().BuildRef(), ActorBuilder.Sam, Now);

        Assert.Null(change);
    }
}
