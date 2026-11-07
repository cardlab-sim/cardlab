using CardLab.Domain.Ports;
using CardLab.TestKit.Contracts;

namespace CardLab.Domain.Tests.Fakes;

// T-U-24 · PR-02 · unit (contract) — runs every CurrentAccountBankContractTests case against the scriptable FakeBank.
public sealed class FakeBankContractTests : CurrentAccountBankContractTests
{
    protected override Task<ICurrentAccountBank> CreateBankAsync(BankAccountRef account, string lastFour, Money openingBalance) =>
        throw new NotImplementedException("PR-02");
}
