import { describe, expect, test } from 'vitest';
import { formatMoney } from './money';

describe('formatMoney', () => {
  // T-W-1 · PR-02 · web unit
  // Given "1234.56", "0", "0.5", "50.00", "1000000.00", When formatMoney, Then "£1,234.56", "£0.00", "£0.50", "£50.00", "£1,000,000.00"; "-1", "abc" throw
  // Catches: missing grouping/decimals or float rounding in displayed amounts (AC-38, NFR-6)
  test.each([
    ['1234.56', '£1,234.56'],
    ['0', '£0.00'],
    ['0.5', '£0.50'],
    ['50.00', '£50.00'],
    ['1000000.00', '£1,000,000.00'],
    ['999999999999999.99', '£999,999,999,999,999.99'],
  ])('T-W-1 formats wire amounts as £1,234.56: %s', (input, expected) => {
    expect(formatMoney(input)).toBe(expected);
  });

  test.each(['-1', 'abc', '', '1.234', '1,000'])('T-W-1 rejects %j', (input) => {
    expect(() => formatMoney(input)).toThrow();
  });
});

describe('formatMoney (002 payment amounts)', () => {
  // T-W-1 · PR-03 · web unit
  // Given "150.50", "170.50", "25000.01", "1000000.01" and "0.00", When formatMoney, Then "£150.50", "£170.50", "£25,000.01", "£1,000,000.01", "£0.00" (the same text the server puts in W-* messages)
  // Catches: SPA amounts disagreeing with the server-rendered wording on the same screen (DEC-10, R-12)
  test.each([
    ['150.50', '£150.50'],
    ['170.50', '£170.50'],
    ['25000.01', '£25,000.01'],
    ['1000000.01', '£1,000,000.01'],
    ['0.00', '£0.00'],
  ])('T-W-1 formats payment amounts like the server wording: %s', (input, expected) => {
    expect(formatMoney(input)).toBe(expected);
  });
});
