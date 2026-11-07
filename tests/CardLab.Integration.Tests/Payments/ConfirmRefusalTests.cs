using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class ConfirmRefusalTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public ConfirmRefusalTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-3 · PR-06 · integration
    // Given a £150.50 quote and then a £20.00 approved purchase, When confirm, Then 409 ProblemDetails {code balance_changed, message W-6 with £170.50, balanceOwed "170.50"}; no Payment row, no BankEntry; confirming again → 409 {code quote_refused, same message}; a new quote has a different reference
    // Catches: paying a stale amount or reusing a refused reference (AC-8, AC-30, R-5, R-7)
    [Fact(Skip = "todo")]
    public void Balance_change_is_refused_and_stays_refused()
    {
    }

    // T-I-3 · PR-06 · integration
    // Given a card owing £0.00, When POST quotes, Then 409 {code nothing_to_pay, message W-7}; a malformed reference, an unknown reference and another card's reference on confirm/check/receipt → 404
    // Catches: quotes for nothing, or references usable across cards (AC-5, design §9, §10)
    [Fact(Skip = "todo")]
    public void Unavailable_quote_and_bad_references_are_rejected()
    {
    }
}
