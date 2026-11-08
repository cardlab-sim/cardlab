namespace CardLab.Infrastructure.FakeBank;

/// <summary>Outcome the fake bank gives the next debit request (R-15, DEC-6). One-shot: cleared when used.</summary>
public enum NextDebitOutcome
{
    /// <summary>Complete (explicitly steered: the outcome is marked simulated).</summary>
    Complete,

    /// <summary>Refuse with insufficient funds.</summary>
    RefuseInsufficientFunds,

    /// <summary>Refuse with account unavailable.</summary>
    RefuseAccountUnavailable,

    /// <summary>Apply the debit (ledger written), then never answer.</summary>
    TimeoutDebitApplied,

    /// <summary>Write nothing, then never answer (a later status check says not received).</summary>
    TimeoutNotReceived,
}

/// <summary>How the fake bank treats the next status check (DEC-7). One-shot: cleared when used.</summary>
public enum NextStatusCheck
{
    /// <summary>Answer from the ledger and request memory (the default).</summary>
    Answers,

    /// <summary>Never answer (the caller's budget fires; the outcome is marked simulated).</summary>
    DoesNotAnswer,
}

/// <summary>The fake bank's one-shot steering (single row <c>BankSteering</c>). Null fields mean "not steered".</summary>
/// <param name="NextDebit">Outcome of the next debit request; null = natural behaviour (complete unless above balance).</param>
/// <param name="NextStatusCheck">Behaviour of the next status check; null = answers.</param>
public sealed record Steering(NextDebitOutcome? NextDebit, NextStatusCheck? NextStatusCheck);

/// <summary>
/// The labelled "Fake bank: next outcome" control behind <c>/api/fake-bank</c> (R-15). Changes only the next outcome;
/// never balances, ledger or records. Implemented by <see cref="SqlFakeBank"/> (PR-05).
/// </summary>
public interface IFakeBankSteering
{
    /// <summary>Reads the current steering.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The steering (both null when nothing is set).</returns>
    Task<Steering> GetAsync(CancellationToken ct = default);

    /// <summary>Replaces the steering (callers merge unspecified fields themselves).</summary>
    /// <param name="steering">New steering.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when stored.</returns>
    Task SetAsync(Steering steering, CancellationToken ct = default);
}
