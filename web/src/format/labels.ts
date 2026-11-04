import { notImplemented } from '../notImplemented';
import type { AuditAction, CardStatus, Category, Outcome, Period, Reason } from '../api/types';

/**
 * Display labels (English UK) for wire values, e.g. `meals_and_entertainment` → "Meals & Entertainment",
 * `day` → "Day", `limit_exceeded` → "Limit exceeded", `limit_changed` → "Limit changed", `frozen` → "Frozen".
 * Reason codes are also shown verbatim next to the label where the UI rules ask for the code (AC-22).
 * Implemented in PR-02.
 */
export function categoryLabel(category: Category): string {
  return notImplemented('PR-02', category);
}

/** @see categoryLabel */
export function periodLabel(period: Period): string {
  return notImplemented('PR-02', period);
}

/** @see categoryLabel */
export function statusLabel(status: CardStatus): string {
  return notImplemented('PR-02', status);
}

/** @see categoryLabel */
export function outcomeLabel(outcome: Outcome): string {
  return notImplemented('PR-02', outcome);
}

/** @see categoryLabel */
export function reasonLabel(reason: Reason): string {
  return notImplemented('PR-02', reason);
}

/** @see categoryLabel */
export function auditActionLabel(action: AuditAction): string {
  return notImplemented('PR-02', action);
}
