using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.Domain.Tests.Fakes;

// T-U-23 · PR-02 · unit (contract) — runs every PaymentStoreContractTests case against InMemoryCardStore.
public sealed class InMemoryPaymentStoreContractTests : PaymentStoreContractTests
{
    protected override Task<ICardStore> CreateStoreAsync(IReadOnlyList<StoreSeedCard> cards, LinkedAccount account) =>
        throw new NotImplementedException("PR-02");

    protected override Task<ICardStore> OpenStoreAsync(ICardStore seeded) => throw new NotImplementedException("PR-02");
}
