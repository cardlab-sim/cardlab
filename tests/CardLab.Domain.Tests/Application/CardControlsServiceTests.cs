namespace CardLab.Domain.Tests.Application;

public sealed class CardControlsServiceTests
{
    // T-U-34 · PR-05 · unit (application)
    // Given an InMemoryCardStore with an active card, FakeClock, FakeActor.Sam, CapturingLogger, When FreezeAsync, Then Ok(Frozen), store status Frozen, one audit record, exactly one ControlChanged log with ActorId, ActorName, CardId, LastFour, Action "freeze"
    // Catches: state saved without audit, or log missing/duplicated (AC-1, AC-39)
    [Fact(Skip = "todo")]
    public void Freeze_persists_status_audit_and_one_log_event()
    {
    }

    // T-U-35 · PR-05 · unit (application)
    // Given a frozen card, When FreezeAsync; and an active card, When UnfreezeAsync, Then Ok with unchanged status, no audit record, no log event
    // Catches: no-op paths writing audit or logs (AC-2, AC-40)
    [Fact(Skip = "todo")]
    public void No_op_freeze_and_unfreeze_write_nothing()
    {
    }

    // T-U-36 · PR-05 · unit (application)
    // Given no card with the id, When FreezeAsync / UnfreezeAsync / SetLimitAsync / RemoveLimitAsync, Then Fail(CardNotFound) and the store has no audit records
    // Catches: exceptions or phantom rows for unknown ids (API 404)
    [Fact(Skip = "todo")]
    public void Unknown_card_is_card_not_found()
    {
    }

    // T-U-37 · PR-05 · unit (application)
    // Given a card, When SetLimitAsync with -1.00, 1000000.01 and 10.005, Then Fail(InvalidAmount) each time, no limit, no audit, no log; £0.00 and £1,000,000.00 succeed
    // Catches: invalid amounts stored or audited (AC-9, R-11)
    [Fact(Skip = "todo")]
    public void Invalid_limit_amounts_are_rejected_without_side_effects()
    {
    }

    // T-U-38 · PR-05 · unit (application)
    // Given Fuel/Day £50.00 set via the service, When SetLimitAsync(Fuel, Day, 80.00), Then exactly one Fuel/Day limit of £80.00 and audit newest-first [LimitChanged 50.00→80.00, LimitSet 50.00]
    // Catches: duplicate limits or wrong action on replace (AC-6, AC-7)
    [Fact(Skip = "todo")]
    public void Set_then_replace_limit_through_the_service()
    {
    }

    // T-U-39 · PR-05 · unit (application)
    // Given Fuel/Day £80.00, When RemoveLimitAsync twice, Then Ok(true) with one LimitRemoved audit and one log, then Ok(false) with nothing new
    // Catches: removal no-op writing audit (AC-8)
    [Fact(Skip = "todo")]
    public void Remove_limit_then_remove_again()
    {
    }
}
