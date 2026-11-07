using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;

namespace CardLab.Domain.Application;

/// <summary>Expected failures of the admin operations (design §5).</summary>
public enum DomainError
{
    /// <summary>No card has the given id (API: 404).</summary>
    CardNotFound,

    /// <summary>Limit amount outside R-11 or with more than 2 dp (API: 400, nothing stored, AC-9).</summary>
    InvalidAmount,
}

/// <summary>What an authorisation produced, for either entry path (DEC-14).</summary>
/// <param name="Decision">The decision (for invalid/unknown: declined with <c>InvalidRequest</c>/<c>CardNotFound</c> and no limits).</param>
/// <param name="TransactionId">Id of the stored transaction; null when nothing was stored (invalid request, unknown card).</param>
/// <param name="Problems">Validation problems; non-empty only for <c>InvalidRequest</c>.</param>
public sealed record AuthorisationResult(Decision Decision, TransactionId? TransactionId, IReadOnlyList<ValidationProblem> Problems);
