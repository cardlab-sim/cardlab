using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class StatusCheckTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public StatusCheckTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-7 · PR-06 · integration
    // Given a pending payment from steering timeout_debit_applied, When POST check (as the SPA does on opening the view), Then 200 completed with time = the bank's debit time, balance "0.00", one BankEntry, and a new quote is allowed
    // Catches: a payment the bank applied staying pending (AC-14, AC-34, R-9)
    [Fact(Skip = "todo")]
    public void Check_completes_a_debit_the_bank_applied()
    {
    }

    // T-I-7 · PR-06 · integration
    // Given a pending payment from steering timeout_not_received, When POST check, Then 200 failed with failureReason not_received and the W-4 text, balance "150.50", no BankEntry, and a new quote is allowed
    // Catches: a never-received debit left pending (AC-15, R-11)
    [Fact(Skip = "todo")]
    public void Check_fails_a_debit_the_bank_never_received()
    {
    }

    // T-I-7 · PR-06 · integration
    // Given a pending payment and nextStatusCheck does_not_answer, When POST check, Then 200 pending with W-10 within 3 s; the bank received no new debit request; a second check answers
    // Catches: unanswered checks failing or re-sending (AC-16, R-11)
    [Fact(Skip = "todo")]
    public void Unanswered_check_stays_pending()
    {
    }

    // T-I-7 · PR-06 · integration
    // Given a pending £150.50 payment (timeout_debit_applied) and then a £20.00 approved purchase, When POST check, Then balance "20.00" and the stored record (ICardStore.GetPaymentRecordAsync from a host scope; the admin endpoint is PR-07) shows BalanceBefore £170.50 and BalanceAfter £20.00
    // Catches: settlement balances computed outside the lock (AC-36, R-14)
    [Fact(Skip = "todo")]
    public void Settlement_records_before_and_after_balances()
    {
    }
}
