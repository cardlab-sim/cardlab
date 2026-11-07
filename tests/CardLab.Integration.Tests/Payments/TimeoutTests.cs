using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class TimeoutTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public TimeoutTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-6 · PR-06 · integration
    // Given BankTimeoutSeconds 1 and steering timeout_not_received, When confirm, Then 200 within 3 s with state pending, fee "0.00", message W-5 (balance £150.50); balance still "150.50"; the view shows pendingPayment with W-5 and availability payment_pending
    // Catches: an unanswered bank failing or blocking the confirm (AC-13, NFR-2, R-11)
    [Fact(Skip = "todo")]
    public void Unanswered_debit_returns_pending_within_budget()
    {
    }

    // T-I-6 · PR-06 · integration
    // Given steering timeout_not_received and a resulting pending payment that a check then resolves as failed, When a second payment is quoted and confirmed with no further steering, Then it completes
    // Catches: steering applying to more than one request (AC-32, R-15)
    [Fact(Skip = "todo")]
    public void Timeout_steering_applies_to_one_request()
    {
    }
}
