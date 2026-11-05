using CardLab.Domain.Application;
using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Limits;
using CardLab.Domain.Periods;
using CardLab.TestKit.Builders;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Application;

public sealed class CardControlsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly CardRef _card = new CardBuilder().WithCardNumber("4000000000000001").BuildRef();
    private readonly CapturingLogger<CardControlsService> _logger = new();
    private readonly FakeClock _clock = new(Now);

    private async Task<(CardControlsService Service, InMemoryCardStore Store)> CreateAsync(bool frozen = false, params SpendLimit[] limits)
    {
        var builder = new CardBuilder().WithId(_card.Id).WithCardNumber("4000000000000001");
        var store = new InMemoryCardStore(frozen ? builder.Frozen().Build() : builder.Build());
        var service = new CardControlsService(store, _clock, FakeActor.Sam, _logger);
        await store.WithCardLockAsync(
            _card.Id,
            w =>
            {
                foreach (var limit in limits)
                {
                    w.UpsertLimit(limit);
                }

                return Task.FromResult(true);
            },
            Ct);

        return (service, store);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CardId UnknownId => new(Guid.Parse("00000000-0000-0000-0000-00000000dead"));

    // T-U-34 · PR-05 · unit (application)
    // Given an InMemoryCardStore with an active card, FakeClock, FakeActor.Sam, CapturingLogger, When FreezeAsync, Then Ok(Frozen), store status Frozen, one audit record, exactly one ControlChanged log with ActorId, ActorName, CardId, LastFour, Action "freeze"; then UnfreezeAsync on the same card: Ok(Active), store Active, newest audit Unfreeze "Frozen"→"Active", a second log with Action "unfreeze"
    // Catches: state saved without audit, or log missing/duplicated (AC-1, AC-39)
    [Fact]
    public async Task Freeze_persists_status_audit_and_one_log_event()
    {
        var (service, store) = await CreateAsync();

        var result = await service.FreezeAsync(_card.Id, Ct);

        Assert.True(result.IsOk);
        Assert.Equal(CardStatus.Frozen, result.Value);
        Assert.Equal(CardStatus.Frozen, (await store.GetCardAsync(_card.Id, Ct))!.Status);
        var audit = Assert.Single(await store.ListAuditAsync(_card.Id, Ct));
        Assert.Equal(new AuditEvent(_card, ActorBuilder.Sam, Now, AuditAction.Freeze, "Active", "Frozen"), audit.Event);
        var log = Assert.Single(_logger.Events("ControlChanged"));
        Assert.Equal(ActorBuilder.Sam.Id.ToString(), log.Fields["ActorId"]);
        Assert.Equal("Sam Ellis", log.Fields["ActorName"]);
        Assert.Equal(_card.Id.Value.ToString(), log.Fields["CardId"]);
        Assert.Equal("0001", log.Fields["LastFour"]);
        Assert.Equal("freeze", log.Fields["Action"]);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var unfreeze = await service.UnfreezeAsync(_card.Id, Ct);

        Assert.True(unfreeze.IsOk);
        Assert.Equal(CardStatus.Active, unfreeze.Value);
        Assert.Equal(CardStatus.Active, (await store.GetCardAsync(_card.Id, Ct))!.Status);
        var auditAfter = await store.ListAuditAsync(_card.Id, Ct);
        Assert.Equal(2, auditAfter.Count);
        Assert.Equal(new AuditEvent(_card, ActorBuilder.Sam, Now.AddMinutes(1), AuditAction.Unfreeze, "Frozen", "Active"), auditAfter[0].Event);
        var logs = _logger.Events("ControlChanged");
        Assert.Equal(2, logs.Count);
        Assert.Equal("unfreeze", logs[1].Fields["Action"]);
    }

    // T-U-35 · PR-05 · unit (application)
    // Given a frozen card, When FreezeAsync; and an active card, When UnfreezeAsync, Then Ok with unchanged status, no audit record, no log event
    // Catches: no-op paths writing audit or logs (AC-2, AC-40)
    [Fact]
    public async Task No_op_freeze_and_unfreeze_write_nothing()
    {
        var (frozenService, frozenStore) = await CreateAsync(frozen: true);
        var (activeService, activeStore) = await CreateAsync();

        var freeze = await frozenService.FreezeAsync(_card.Id, Ct);
        var unfreeze = await activeService.UnfreezeAsync(_card.Id, Ct);

        Assert.True(freeze.IsOk);
        Assert.Equal(CardStatus.Frozen, freeze.Value);
        Assert.True(unfreeze.IsOk);
        Assert.Equal(CardStatus.Active, unfreeze.Value);
        Assert.Equal(0, frozenStore.AuditCount);
        Assert.Equal(0, activeStore.AuditCount);
        Assert.Empty(_logger.Events("ControlChanged"));
    }

    // T-U-36 · PR-05 · unit (application)
    // Given no card with the id, When FreezeAsync / UnfreezeAsync / SetLimitAsync / RemoveLimitAsync, Then Fail(CardNotFound) and the store has no audit records
    // Catches: exceptions or phantom rows for unknown ids (API 404)
    [Fact]
    public async Task Unknown_card_is_card_not_found()
    {
        var (service, store) = await CreateAsync();
        var id = UnknownId;

        var freeze = await service.FreezeAsync(id, Ct);
        var unfreeze = await service.UnfreezeAsync(id, Ct);
        var set = await service.SetLimitAsync(id, Category.Fuel, Period.Day, 50.00m, Ct);
        var remove = await service.RemoveLimitAsync(id, Category.Fuel, Period.Day, Ct);

        Assert.False(freeze.IsOk);
        Assert.Equal(DomainError.CardNotFound, freeze.Error);
        Assert.False(unfreeze.IsOk);
        Assert.Equal(DomainError.CardNotFound, unfreeze.Error);
        Assert.False(set.IsOk);
        Assert.Equal(DomainError.CardNotFound, set.Error);
        Assert.False(remove.IsOk);
        Assert.Equal(DomainError.CardNotFound, remove.Error);
        Assert.Equal(0, store.AuditCount);
        Assert.Empty(_logger.Events("ControlChanged"));
    }

    // T-U-37 · PR-05 · unit (application)
    // Given a card, When SetLimitAsync with -1.00, 1000000.01 and 10.005, Then Fail(InvalidAmount) each time, no limit, no audit, no log; £0.00 and £1,000,000.00 succeed
    // Catches: invalid amounts stored or audited (AC-9, R-11)
    [Fact]
    public async Task Invalid_limit_amounts_are_rejected_without_side_effects()
    {
        var (service, store) = await CreateAsync();

        foreach (var amount in new[] { -1.00m, 1000000.01m, 10.005m })
        {
            var result = await service.SetLimitAsync(_card.Id, Category.Fuel, Period.Day, amount, Ct);

            Assert.False(result.IsOk);
            Assert.Equal(DomainError.InvalidAmount, result.Error);
        }

        Assert.Empty((await store.GetCardAsync(_card.Id, Ct))!.Limits);
        Assert.Equal(0, store.AuditCount);
        Assert.Empty(_logger.Events("ControlChanged"));

        var zero = await service.SetLimitAsync(_card.Id, Category.Fuel, Period.Day, 0.00m, Ct);
        var max = await service.SetLimitAsync(_card.Id, Category.Fuel, Period.Week, 1000000.00m, Ct);

        Assert.True(zero.IsOk);
        Assert.Equal("0.00", zero.Value.Amount.ToString());
        Assert.True(max.IsOk);
        Assert.Equal("1000000.00", max.Value.Amount.ToString());
    }

    // T-U-38 · PR-05 · unit (application)
    // Given Fuel/Day £50.00 set via the service, When SetLimitAsync(Fuel, Day, 80.00), Then exactly one Fuel/Day limit of £80.00 and audit newest-first [LimitChanged 50.00→80.00, LimitSet 50.00]
    // Catches: duplicate limits or wrong action on replace (AC-6, AC-7)
    [Fact]
    public async Task Set_then_replace_limit_through_the_service()
    {
        var (service, store) = await CreateAsync();

        var first = await service.SetLimitAsync(_card.Id, Category.Fuel, Period.Day, 50.00m, Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var second = await service.SetLimitAsync(_card.Id, Category.Fuel, Period.Day, 80.00m, Ct);

        Assert.True(first.IsOk);
        Assert.True(second.IsOk);
        var limit = Assert.Single((await store.GetCardAsync(_card.Id, Ct))!.Limits);
        Assert.Equal(new LimitBuilder().Of("80.00").Build(), limit);
        var audit = await store.ListAuditAsync(_card.Id, Ct);
        Assert.Equal(2, audit.Count);
        Assert.Equal(new AuditEvent(_card, ActorBuilder.Sam, Now.AddMinutes(1), AuditAction.LimitChanged, "Fuel/Day/50.00", "Fuel/Day/80.00"), audit[0].Event);
        Assert.Equal(new AuditEvent(_card, ActorBuilder.Sam, Now, AuditAction.LimitSet, null, "Fuel/Day/50.00"), audit[1].Event);
        Assert.Equal(["limit_set", "limit_changed"], _logger.Events("ControlChanged").Select(e => e.Fields["Action"]));
    }

    // T-U-39 · PR-05 · unit (application)
    // Given Fuel/Day £80.00, When RemoveLimitAsync twice, Then Ok(true) with one LimitRemoved audit and one log, then Ok(false) with nothing new
    // Catches: removal no-op writing audit (AC-8)
    [Fact]
    public async Task Remove_limit_then_remove_again()
    {
        var (service, store) = await CreateAsync(limits: new LimitBuilder().Of("80.00").Build());
        _clock.Advance(TimeSpan.FromMinutes(1));

        var first = await service.RemoveLimitAsync(_card.Id, Category.Fuel, Period.Day, Ct);
        var auditAfterFirst = await store.ListAuditAsync(_card.Id, Ct);
        var second = await service.RemoveLimitAsync(_card.Id, Category.Fuel, Period.Day, Ct);

        Assert.True(first.IsOk);
        Assert.True(first.Value);
        Assert.Empty((await store.GetCardAsync(_card.Id, Ct))!.Limits);
        var removed = Assert.Single(auditAfterFirst);
        Assert.Equal(new AuditEvent(_card, ActorBuilder.Sam, Now.AddMinutes(1), AuditAction.LimitRemoved, "Fuel/Day/80.00", null), removed.Event);
        Assert.Equal("limit_removed", Assert.Single(_logger.Events("ControlChanged")).Fields["Action"]);
        Assert.True(second.IsOk);
        Assert.False(second.Value);
        Assert.Equal(1, store.AuditCount);
    }
}
