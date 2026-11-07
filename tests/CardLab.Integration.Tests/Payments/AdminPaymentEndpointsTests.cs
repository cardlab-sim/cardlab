using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class AdminPaymentEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public AdminPaymentEndpointsTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-19 · PR-07 · integration
    // Given Ops van with approved £120.00 and £30.50 and declined £500.00, When GET /api/cards/{id}, Then balanceOwed "150.50"; after a completed £150.50 payment (arranged through PaymentService) it is "0.00"
    // Catches: the admin not seeing the balance (AC-1, AC-2, R-1)
    [Fact(Skip = "todo")]
    public void Card_detail_shows_balance_owed()
    {
    }

    // T-I-19 · PR-07 · integration
    // Given purchases and a completed payment, When GET /api/cards/{id}/activity and ?take=2, Then newest first, the payment row {kind payment, state completed, reference, origin cardholder_view, outcome null} and purchase rows {kind purchase, outcome, category, reason}; take honoured; unknown card → 404
    // Catches: a payment listed as a purchase or the history losing 001 columns (AC-2, R-13, DEC-12)
    [Fact(Skip = "todo")]
    public void Activity_lists_payments_as_payments()
    {
    }

    // T-I-19 · PR-07 · integration
    // Given an open quote, a refused reference and a completed payment, When GET /api/cards/{id}/payments, Then three items newest first with status open/refused/confirmed, state null/null/completed, origin, settledAt only for the settled one
    // Catches: refused or open references missing from the admin list (R-14)
    [Fact(Skip = "todo")]
    public void Payments_list_includes_every_reference()
    {
    }
}
