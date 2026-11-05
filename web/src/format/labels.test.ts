import { describe, expect, test } from 'vitest';
import type { AuditAction, CardStatus, Category, Outcome, Period, Reason } from '../api/types';
import {
  auditActionLabel,
  categoryLabel,
  outcomeLabel,
  periodLabel,
  reasonLabel,
  statusLabel,
} from './labels';

const categories: Category[] = [
  'travel',
  'fuel',
  'meals_and_entertainment',
  'software_and_subscriptions',
  'office_supplies',
  'advertising',
  'professional_services',
  'other',
];
const periods: Period[] = ['day', 'week', 'month'];
const statuses: CardStatus[] = ['active', 'frozen'];
const outcomes: Outcome[] = ['approved', 'declined'];
const reasons: Reason[] = [
  'approved',
  'card_frozen',
  'limit_exceeded',
  'card_not_found',
  'invalid_request',
];
const actions: AuditAction[] = [
  'freeze',
  'unfreeze',
  'limit_set',
  'limit_changed',
  'limit_removed',
];

function expectDistinctReadable<T extends string>(values: T[], label: (v: T) => string): void {
  const labels = values.map(label);
  for (const [i, text] of labels.entries()) {
    expect(text, `label of ${values[i]}`).toMatch(/^[A-Z][A-Za-z &]*$/);
  }
  expect(new Set(labels).size).toBe(values.length);
}

describe('labels', () => {
  // T-W-4 · PR-02 · web unit
  // Given every Category, Period, CardStatus, Outcome, Reason and AuditAction value, When labelled, Then a non-empty English label (e.g. "Meals & Entertainment", "Limit exceeded", "Limit changed") and no label equals another within its type
  // Catches: raw wire names or undefined shown to the admin
  test('T-W-4 labels every wire value', () => {
    expectDistinctReadable(categories, categoryLabel);
    expectDistinctReadable(periods, periodLabel);
    expectDistinctReadable(statuses, statusLabel);
    expectDistinctReadable(outcomes, outcomeLabel);
    expectDistinctReadable(reasons, reasonLabel);
    expectDistinctReadable(actions, auditActionLabel);
    expect(categoryLabel('meals_and_entertainment')).toBe('Meals & Entertainment');
    expect(reasonLabel('limit_exceeded')).toBe('Limit exceeded');
    expect(auditActionLabel('limit_changed')).toBe('Limit changed');
    expect(statusLabel('frozen')).toBe('Frozen');
    expect(periodLabel('day')).toBe('Day');
  });
});
