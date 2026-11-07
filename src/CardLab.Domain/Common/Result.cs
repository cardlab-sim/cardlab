using System.Diagnostics.CodeAnalysis;

namespace CardLab.Domain.Common;

/// <summary>
/// Outcome of an operation that either succeeds with a value or fails with an error, so expected
/// failures (unknown card, invalid amount, malformed request) are values rather than exceptions.
/// </summary>
/// <remarks>Complete in the skeleton (PR-00): a shared primitive with no domain rules.</remarks>
/// <typeparam name="T">Success value type.</typeparam>
/// <typeparam name="TError">Error type (an enum or a list of problems).</typeparam>
public sealed class Result<T, TError>
{
    private Result(bool isOk, T? value, TError? error)
    {
        IsOk = isOk;
        Value = value;
        Error = error;
    }

    /// <summary>True when the operation succeeded and <see cref="Value"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsOk { get; }

    /// <summary>The success value; default when <see cref="IsOk"/> is false.</summary>
    public T? Value { get; }

    /// <summary>The error; default when <see cref="IsOk"/> is true.</summary>
    public TError? Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The value produced.</param>
    /// <returns>A result with <see cref="IsOk"/> true.</returns>
    public static Result<T, TError> Ok(T value) => new(true, value, default);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">Why the operation failed.</param>
    /// <returns>A result with <see cref="IsOk"/> false.</returns>
    public static Result<T, TError> Fail(TError error) => new(false, default, error);
}
