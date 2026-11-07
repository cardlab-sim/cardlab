using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class ConfirmEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public ConfirmEndpointTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-2 · PR-06 · integration
    // Given Ops van owing £150.50, When POST quotes then POST confirm, Then 201 quote with the W-1 lines and 200 receipt {state completed, fee "0.00", message W-2, balanceOwed "0.00", repeated false}; the view then shows balance "0.00", account available "24849.50", an accountActivity entry with the same reference and amount, and a cardActivity row {kind payment, state completed}
    // Catches: money moved on one side only or the payment missing from history (AC-2, AC-7, R-9, R-13)
    [Fact(Skip = "todo")]
    public void Full_balance_payment_completes_on_both_sides()
    {
    }

    // T-I-2 · PR-06 · integration
    // Given a card owing £25,000.01 and the fake bank at £25,000.00 (no steering), When quote and confirm, Then receipt failed with failureReason insufficient_funds and W-3; balance still "25000.01"; account still "25000.00"
    // Catches: an overdrawing payment accepted (AC-31, R-15)
    [Fact(Skip = "todo")]
    public void Debit_above_available_balance_fails_naturally()
    {
    }
}
