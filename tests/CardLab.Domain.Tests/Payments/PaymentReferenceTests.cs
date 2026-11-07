namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentReferenceTests
{
    // T-U-8 · PR-01 · unit
    // Given "PAY-00000001", "PAY-7K3M9Q2T", When TryParse, Then true with the same Value and ToString; Given null, "", "pay-00000001", "PAY-0000001", "PAY-000000001", "PAY-0000000I", "PAY-0000000L", "PAY-0000000O", "PAY-0000000U", " PAY-00000001", "PAY-00000001\n", Then false
    // Catches: malformed or confusable references reaching the store (DEC-8, design §10)
    [Fact(Skip = "todo")]
    public void TryParse_accepts_only_the_Crockford_format()
    {
    }
}
