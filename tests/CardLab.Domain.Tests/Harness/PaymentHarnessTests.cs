namespace CardLab.Domain.Tests.Harness;

public sealed class PaymentHarnessTests
{
    // T-U-37 · PR-02 · unit
    // Given a FakeBank, When NextDebit(Refused) then two natural debits, Hang(debitApplied: true), FailNext(error), HangStatus and HoldDebitsUntil(gate) with two concurrent debits, Then the script applies once and reverts; Hang(true) writes the entry then throws OperationCanceledException and a status check then says Completed; FailNext throws the error once; HangStatus throws once; both held debits answer only after the gate completes; DebitRequests and StatusChecks count every call
    // Catches: a fake that drifts from the scripts the application tests rely on (AC-10, AC-13, AC-16)
    [Fact(Skip = "todo")]
    public void FakeBank_scripts_are_one_shot()
    {
    }

    // T-U-37 · PR-02 · unit
    // Given FakePaymentReferences, When Next three times (also from parallel callers), Then PAY-00000001, PAY-00000002, PAY-00000003 (distinct, each matching PaymentReference.Pattern) and Issued lists them
    // Catches: non-deterministic or invalid references in unit tests (DEC-8)
    [Fact(Skip = "todo")]
    public void FakePaymentReferences_are_sequential_and_valid()
    {
    }

    // T-U-37 · PR-02 · unit
    // Given text with "51907731", a 16-digit card number, the BankAccountRef GUID and "25000.00", When FindAccountNumbers / AssertNoFullNumbers, Then only the account number is found by FindAccountNumbers, and AssertNoFullNumbers throws for the account number or the card number (message shows last fours only) but not for the GUID or amounts
    // Catches: a scanner too weak to catch the account number, or one that flags GUIDs (NFR-3, AC-23)
    [Fact(Skip = "todo")]
    public void PanScanner_finds_the_seeded_account_number()
    {
    }
}
