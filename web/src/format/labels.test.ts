import { describe, test } from 'vitest';

describe('labels', () => {
  // T-W-4 · PR-02 · web unit
  // Given every Category, Period, CardStatus, Outcome, Reason and AuditAction value, When labelled, Then a non-empty English label (e.g. "Meals & Entertainment", "Limit exceeded", "Limit changed") and no label equals another within its type
  // Catches: raw wire names or undefined shown to the admin
  test.todo('T-W-4 labels every wire value');
});
