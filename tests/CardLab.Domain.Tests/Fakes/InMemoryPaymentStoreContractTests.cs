using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Fakes;

// T-U-23 · PR-02 · unit (contract) — runs every PaymentStoreContractTests case against InMemoryCardStore.
public sealed class InMemoryPaymentStoreContractTests : PaymentStoreContractTests
{
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards, LinkedAccount account) =>
        Task.FromResult<ICardStore>(new InMemoryCardStore(cards).WithLinkedAccount(account));

    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) => Task.FromResult(seeded);
}
