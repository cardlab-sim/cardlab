using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class FakeBankSteeringTests : IClassFixture<SqlTestDatabase>
{
    private readonly SqlTestDatabase _fixture;

    public FakeBankSteeringTests(SqlTestDatabase fixture) => _fixture = fixture;

    // T-I-20 · PR-05 · integration
    // Given SqlFakeBank with steering RefuseAccountUnavailable, When two debits are requested, Then the first is Refused(AccountUnavailable, simulated) with nothing written and the second completes naturally (not simulated); steering Complete marks the completion simulated
    // Catches: steering applying more than once or steered outcomes not marked (AC-32, AC-33, R-15)
    [Fact(Skip = "todo")]
    public void Steering_is_one_shot_and_marks_simulated()
    {
    }

    // T-I-20 · PR-05 · integration
    // Given steering TimeoutDebitApplied, When a debit is requested with a 1 s token, Then OperationCanceledException after ~1 s, yet the entry and the reduced balance are committed and a later status check says Completed with the debit time; with TimeoutNotReceived nothing is written and the status check says NotReceived
    // Catches: timeout modes writing the wrong side or returning instead of hanging (AC-13, AC-14, AC-15, DEC-6)
    [Fact(Skip = "todo")]
    public void Timeout_modes_hang_after_writing_or_not()
    {
    }

    // T-I-20 · PR-05 · integration
    // Given NextStatusCheck DoesNotAnswer, When two status checks run (1 s token), Then the first throws OperationCanceledException (simulated) and the second answers; steering never changes balance or entries
    // Catches: unanswered status checks unreachable for AC-16 (DEC-7)
    [Fact(Skip = "todo")]
    public void Status_check_steering_is_one_shot()
    {
    }
}
