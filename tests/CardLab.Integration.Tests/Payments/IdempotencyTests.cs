using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class IdempotencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _fixture;

    public IdempotencyTests(ApiFactory fixture) => _fixture = fixture;

    // T-I-4 · PR-06 · integration
    // Given a completed confirm, When the same confirm is sent twice more, Then each 200 body equals the first except repeated=true; one Payment row; one BankEntry; the stored record (ICardStore.GetPaymentRecordAsync from a host scope) lists two ConfirmRepeated events
    // Catches: a resubmit creating a second payment or debit (AC-9, R-7)
    [Fact(Skip = "todo")]
    public void Repeated_confirm_returns_the_original_receipt()
    {
    }

    // T-I-5 · PR-06 · integration
    // Given one quote, When 20 confirms of the same reference are sent in parallel, Then all return 200 with state completed and the same reference, exactly one Payment row and one BankEntry exist
    // Catches: concurrent confirms racing past the rules or the unique indexes (AC-10, DEC-5)
    [Fact(Skip = "todo")]
    public void Parallel_confirms_create_one_payment()
    {
    }
}
