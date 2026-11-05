using System.Globalization;

namespace CardLab.Domain;

/// <summary>
/// A non-negative GBP amount with at most two decimal places, exact to the penny (R-11).
/// </summary>
/// <remarks>
/// Rules (design §5): construction only through <see cref="TryFromDecimal"/>, which rejects more
/// than 2 dp and negative values (PLANNER-DECISION: Money is never negative, consistent with
/// subtraction flooring at zero); <c>-</c> floors at <see cref="Zero"/>; comparisons by amount;
/// <see cref="ToString"/> is invariant with exactly two decimals (<c>"1234.56"</c>), which is the
/// wire format. Equal amounts are equal regardless of decimal scale (50.0 == 50.00).
/// Implemented in PR-01. High-risk path.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    private Money(decimal amount) => Amount = amount;

    /// <summary>The amount in pounds, at most two decimal places, never negative.</summary>
    public decimal Amount { get; }

    /// <summary>£0.00.</summary>
    public static Money Zero => default;

    /// <summary>Creates a Money from a decimal.</summary>
    /// <param name="value">Amount in pounds.</param>
    /// <param name="money">The money value when the method returns true; <see cref="Zero"/> otherwise.</param>
    /// <returns>False when <paramref name="value"/> has more than two decimal places or is negative.</returns>
    public static bool TryFromDecimal(decimal value, out Money money)
    {
        if (value < 0m || decimal.Round(value, 2) != value)
        {
            money = Zero;
            return false;
        }

        // Adding 0.00m normalises the scale to 2, so 50.0 and 50.00 are stored identically.
        money = new Money(value + 0.00m);
        return true;
    }

    /// <summary>Adds two amounts.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>The exact sum.</returns>
    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    /// <summary>Subtracts, flooring at £0.00 (used for "remaining", which is never negative).</summary>
    /// <param name="left">Minuend.</param>
    /// <param name="right">Subtrahend.</param>
    /// <returns><c>max(0, left - right)</c>.</returns>
    public static Money operator -(Money left, Money right) => left.Amount > right.Amount ? new Money(left.Amount - right.Amount) : Zero;

    /// <summary>The larger of two amounts.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>The larger amount.</returns>
    public static Money Max(Money left, Money right) => left.Amount >= right.Amount ? left : right;

    /// <summary>Strictly less than.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>True when <paramref name="left"/> is smaller.</returns>
    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    /// <summary>Strictly greater than.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>True when <paramref name="left"/> is larger.</returns>
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    /// <summary>Less than or equal.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>True when <paramref name="left"/> is not larger.</returns>
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    /// <summary>Greater than or equal.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    /// <returns>True when <paramref name="left"/> is not smaller.</returns>
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    /// <summary>Orders by amount.</summary>
    /// <param name="other">The amount to compare with.</param>
    /// <returns>Negative, zero or positive as for <see cref="decimal.CompareTo(decimal)"/>.</returns>
    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    /// <summary>Invariant two-decimal text, e.g. <c>"1234.56"</c>, <c>"0.00"</c> (the wire format).</summary>
    /// <returns>The amount with exactly two decimals and no thousands separator.</returns>
    public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture);
}
