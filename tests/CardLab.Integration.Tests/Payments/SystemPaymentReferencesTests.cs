using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class SystemPaymentReferencesTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _fixture;

    public SystemPaymentReferencesTests(SqlTestDatabase fixture) => _fixture = fixture;

    // T-I-23 · PR-05 · integration
    // Given SystemPaymentReferences, When Next 1,000 times, Then every value matches PaymentReference.Pattern and all are distinct
    // Catches: references outside the format or from a weak source (DEC-8)
    [Fact(Skip = "todo")]
    public void Mints_valid_distinct_references()
    {
    }
}
