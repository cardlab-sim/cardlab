import { describe, test } from 'vitest';

describe('formatMoney', () => {
  // T-W-1 · PR-02 · web unit
  // Given "1234.56", "0", "0.5", "50.00", "1000000.00", When formatMoney, Then "£1,234.56", "£0.00", "£0.50", "£50.00", "£1,000,000.00"; "-1", "abc" throw
  // Catches: missing grouping/decimals or float rounding in displayed amounts (AC-38, NFR-6)
  test.todo('T-W-1 formats wire amounts as £1,234.56');
});
