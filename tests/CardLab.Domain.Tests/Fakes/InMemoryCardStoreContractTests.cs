using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Fakes;

// T-U-21 · PR-01 · unit (contract) — runs every CardStoreContractTests case against InMemoryCardStore.
public sealed class InMemoryCardStoreContractTests : CardStoreContractTests
{
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards) =>
        Task.FromResult<ICardStore>(new InMemoryCardStore(cards));

    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) => Task.FromResult(seeded);
}
