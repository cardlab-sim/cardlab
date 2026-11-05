/**
 * Formats a wire amount for display (NFR-6, AC-38): `"1234.56"` → `"£1,234.56"`, `"0"` → `"£0.00"`,
 * `"1000000.00"` → `"£1,000,000.00"`. Always two decimals, en-GB grouping, no floating-point rounding
 * (work on the string, not on a JS number).
 * @param amount - Non-negative decimal string from the API (at most two decimals).
 * @returns The display text.
 * @throws Error when `amount` is not a plain non-negative decimal string.
 */
export function formatMoney(amount: string): string {
  const match = /^(\d+)(?:\.(\d{1,2}))?$/.exec(amount);
  if (match === null) {
    throw new Error(`Not a valid amount: ${amount}`);
  }
  const whole = (match[1] ?? '').replace(/\B(?=(\d{3})+$)/g, ',');
  const fraction = (match[2] ?? '').padEnd(2, '0');
  return `£${whole}.${fraction}`;
}
