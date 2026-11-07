using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class BothSidesTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public BothSidesTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-8 · PR-08 · integration
    // Given J-1 and J-3 run through the API with complete, refuse_insufficient_funds, refuse_account_unavailable, timeout_debit_applied and timeout_not_received (then checks), When after every step the cardholder view (balance, cardActivity) and GET /api/fake-bank (balance, entries) are read, Then a payment is never reflected on one side only (pending appears only on the card side, not counted), and no fee or charge appears anywhere other than "0.00"
    // Catches: a half-applied payment observable at any step (AC-18, R-9, OB-8)
    [Fact(Skip = "todo")]
    public void Both_sides_agree_after_every_step()
    {
    }
}
