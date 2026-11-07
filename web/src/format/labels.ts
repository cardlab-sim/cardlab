import { CATEGORIES, PERIODS } from '../api/types';
import type { AuditAction, CardStatus, Category, Outcome, Period, Reason } from '../api/types';

const STATUS_LABELS: Record<CardStatus, string> = { active: 'Active', frozen: 'Frozen' };

const OUTCOME_LABELS: Record<Outcome, string> = { approved: 'Approved', declined: 'Declined' };

const REASON_LABELS: Record<Reason, string> = {
  approved: 'Approved',
  card_frozen: 'Card frozen',
  limit_exceeded: 'Limit exceeded',
  card_not_found: 'Card not found',
  invalid_request: 'Invalid request',
};

const AUDIT_ACTION_LABELS: Record<AuditAction, string> = {
  freeze: 'Card frozen',
  unfreeze: 'Card unfrozen',
  limit_set: 'Limit set',
  limit_changed: 'Limit changed',
  limit_removed: 'Limit removed',
};

/**
 * Display labels (English UK) for wire values, e.g. `meals_and_entertainment` → "Meals & Entertainment",
 * `day` → "Day", `limit_exceeded` → "Limit exceeded", `limit_changed` → "Limit changed", `frozen` → "Frozen".
 * Reason codes are also shown verbatim next to the label where the UI rules ask for the code (AC-22).
 */
export function categoryLabel(category: Category): string {
  return CATEGORIES.find((c) => c.value === category)?.label ?? category;
}

/** @see categoryLabel */
export function periodLabel(period: Period): string {
  return PERIODS.find((p) => p.value === period)?.label ?? period;
}

/** @see categoryLabel */
export function statusLabel(status: CardStatus): string {
  return STATUS_LABELS[status];
}

/** @see categoryLabel */
export function outcomeLabel(outcome: Outcome): string {
  return OUTCOME_LABELS[outcome];
}

/** @see categoryLabel */
export function reasonLabel(reason: Reason): string {
  return REASON_LABELS[reason];
}

/** @see categoryLabel */
export function auditActionLabel(action: AuditAction): string {
  return AUDIT_ACTION_LABELS[action];
}
