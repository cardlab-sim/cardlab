using CardLab.Domain.Payments;
using CardLab.Infrastructure.Payments;
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
    [Fact]
    public void Mints_valid_distinct_references()
    {
        var source = new SystemPaymentReferences();

        var references = Enumerable.Range(0, 1000).Select(_ => source.Next().Value).ToList();

        Assert.All(references, r => Assert.True(PaymentReference.TryParse(r, out _), r));
        Assert.Equal(1000, references.Distinct().Count());
    }
}
