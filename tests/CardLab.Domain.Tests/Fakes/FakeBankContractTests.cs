using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;
using CardLab.TestKit.Fakes;

namespace CardLab.Domain.Tests.Fakes;

// T-U-24 · PR-02 · unit (contract) — runs every CurrentAccountBankContractTests case against the scriptable FakeBank.
public sealed class FakeBankContractTests : CurrentAccountBankContractTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

    protected override Task<ICurrentAccountBank> CreateBankAsync(BankAccountRef account, string lastFour, Money openingBalance) =>
        Task.FromResult<ICurrentAccountBank>(new FakeBank(new FakeClock(T0), account, lastFour, openingBalance));
}
