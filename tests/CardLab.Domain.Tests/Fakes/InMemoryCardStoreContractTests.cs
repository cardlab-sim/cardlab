using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.Domain.Tests.Fakes;

// T-U-21 · PR-01 · unit (contract) — runs every CardStoreContractTests case against InMemoryCardStore.
public sealed class InMemoryCardStoreContractTests : CardStoreContractTests
{
    /// <inheritdoc />
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards) => throw new NotImplementedException("PR-01");
}
