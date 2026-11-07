using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class LimitsUnaffectedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public LimitsUnaffectedTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-9 · PR-06 · integration
    // Given Ops van with limit Fuel/day £50.00, £40.00 Fuel approved today and a completed £40.00 payment, When a £20.00 Fuel purchase is authorised today, Then declined limit_exceeded
    // Catches: payments reducing category spend (AC-19, R-13)
    [Fact(Skip = "todo")]
    public void Payments_do_not_reduce_category_spend()
    {
    }
}
