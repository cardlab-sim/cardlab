using CardLab.Domain.Payments;

namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentReferenceTests
{
    // T-U-8 · PR-01 · unit
    // Given "PAY-00000001", "PAY-7K3M9Q2T", When TryParse, Then true with the same Value and ToString; Given null, "", "pay-00000001", "PAY-0000001", "PAY-000000001", "PAY-0000000I", "PAY-0000000L", "PAY-0000000O", "PAY-0000000U", " PAY-00000001", "PAY-00000001\n", Then false
    // Catches: malformed or confusable references reaching the store (DEC-8, design §10)
    [Theory]
    [InlineData("PAY-00000001")]
    [InlineData("PAY-7K3M9Q2T")]
    public void TryParse_accepts_valid_references(string text)
    {
        Assert.True(PaymentReference.TryParse(text, out var reference));
        Assert.Equal(text, reference.Value);
        Assert.Equal(text, reference.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pay-00000001")]
    [InlineData("PAY-0000001")]
    [InlineData("PAY-000000001")]
    [InlineData("PAY-0000000I")]
    [InlineData("PAY-0000000L")]
    [InlineData("PAY-0000000O")]
    [InlineData("PAY-0000000U")]
    [InlineData(" PAY-00000001")]
    [InlineData("PAY-00000001\n")]
    public void TryParse_rejects_malformed_or_confusable_references(string? text)
    {
        Assert.False(PaymentReference.TryParse(text, out var reference));
        Assert.Equal(default, reference);
    }
}
