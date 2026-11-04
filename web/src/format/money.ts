import { notImplemented } from '../notImplemented';

/**
 * Formats a wire amount for display (NFR-6, AC-38): `"1234.56"` → `"£1,234.56"`, `"0"` → `"£0.00"`,
 * `"1000000.00"` → `"£1,000,000.00"`. Always two decimals, en-GB grouping, no floating-point rounding
 * (work on the string, not on a JS number).
 * @param amount - Non-negative decimal string from the API.
 * @returns The display text.
 * @throws Error when `amount` is not a plain non-negative decimal string.
 * Implemented in PR-02.
 */
export function formatMoney(amount: string): string {
  return notImplemented('PR-02', amount);
}
