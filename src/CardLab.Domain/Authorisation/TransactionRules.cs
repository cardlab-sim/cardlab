using CardLab.Domain.Common;

namespace CardLab.Domain.Authorisation;

/// <summary>
/// Validation of incoming transaction requests (R-7, R-11, AC-20, design §9). Pure; no I/O.
/// </summary>
/// <remarks>
/// Rules, each producing one <see cref="ValidationProblem"/> (all problems are reported, not just the first):
/// <list type="bullet">
/// <item><c>cardNumber</c> (processor path only): required; exactly 16 ASCII digits, no spaces or separators.</item>
/// <item><c>amount</c>: required; <see cref="WireNames.TryParseAmount"/>; <see cref="Money.TryFromDecimal"/>
/// (at most 2 dp, not negative); <see cref="MoneyBounds.IsValidTransactionAmount"/> (£0.01..£1,000,000.00).</item>
/// <item><c>category</c>: required; <see cref="WireNames.TryParseCategory"/>.</item>
/// <item><c>merchant</c>: required; non-blank after trimming; at most 200 characters.</item>
/// <item><c>timestamp</c>: required on the processor path, optional on the simulate path; ISO-8601 with an
/// explicit offset or <c>Z</c> (text without an offset is a problem); truncated to whole seconds.</item>
/// </list>
/// Messages never echo the submitted card number. Implemented in PR-04. High-risk path.
/// </remarks>
public static class TransactionRules
{
    /// <summary>Validates a processor request.</summary>
    /// <param name="raw">The request as received.</param>
    /// <returns>The validated request, or every problem found (non-empty). A failure maps to reason <c>invalid_request</c>.</returns>
    public static Result<ValidatedAuthorisation, IReadOnlyList<ValidationProblem>> Validate(AuthorisationRequest raw) => throw new NotImplementedException("PR-04");

    /// <summary>Validates a simulated purchase (card already identified by id).</summary>
    /// <param name="raw">The purchase as received.</param>
    /// <param name="defaultOccurredAt">Used when <c>raw.Timestamp</c> is null or empty (the service passes <c>IClock.UtcNow</c>).</param>
    /// <returns>The validated purchase, or every problem found (non-empty).</returns>
    public static Result<ValidatedPurchase, IReadOnlyList<ValidationProblem>> ValidatePurchase(SimulatedPurchase raw, DateTimeOffset defaultOccurredAt) => throw new NotImplementedException("PR-04");
}
