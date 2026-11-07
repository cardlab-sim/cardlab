import { CATEGORIES, PERIODS } from '../api/types';
import type {
  ActivityKind,
  AuditAction,
  CardStatus,
  Category,
  FailureReason,
  NextDebitOutcome,
  NextStatusCheck,
  Outcome,
  PaymentOrigin,
  PaymentState,
  Period,
  QuoteStatus,
  Reason,
} from '../api/types';

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

// 002 (complete in PR-00): badge and control labels. Customer-facing payment messages are NOT built here; they come
// from the server verbatim (DEC-10).

const PAYMENT_STATE_LABELS: Record<PaymentState, string> = {
  pending: 'Pending',
  completed: 'Completed',
  failed: 'Failed',
};

const FAILURE_REASON_LABELS: Record<FailureReason, string> = {
  insufficient_funds: 'Insufficient funds',
  account_unavailable: 'Account unavailable',
  not_received: 'Not received',
};

const ACTIVITY_KIND_LABELS: Record<ActivityKind, string> = {
  purchase: 'Purchase',
  payment: 'Payment',
};

const ORIGIN_LABELS: Record<PaymentOrigin, string> = {
  cardholder_view: 'Cardholder view',
  deploy_smoke: 'origin: smoke',
};

const QUOTE_STATUS_LABELS: Record<QuoteStatus, string> = {
  open: 'Open',
  confirmed: 'Confirmed',
  refused: 'Refused',
};

/** Option labels of the "Fake bank: next outcome" control, in R-15 order. */
export const NEXT_DEBIT_OUTCOMES: readonly { value: NextDebitOutcome; label: string }[] = [
  { value: 'complete', label: 'Complete' },
  { value: 'refuse_insufficient_funds', label: 'Refuse (insufficient funds)' },
  { value: 'refuse_account_unavailable', label: 'Refuse (account unavailable)' },
  { value: 'timeout_debit_applied', label: 'Time out, debit applied' },
  { value: 'timeout_not_received', label: 'Time out, debit not received' },
];

/** Option labels of the "Next status check" control (DEC-7). */
export const NEXT_STATUS_CHECKS: readonly { value: NextStatusCheck; label: string }[] = [
  { value: 'answers', label: 'Answers' },
  { value: 'does_not_answer', label: 'Does not answer' },
];

/** @see categoryLabel */
export function paymentStateLabel(state: PaymentState): string {
  return PAYMENT_STATE_LABELS[state];
}

/** @see categoryLabel */
export function failureReasonLabel(reason: FailureReason): string {
  return FAILURE_REASON_LABELS[reason];
}

/** @see categoryLabel */
export function activityKindLabel(kind: ActivityKind): string {
  return ACTIVITY_KIND_LABELS[kind];
}

/** Origin badge text (DEC-15): `deploy_smoke` → "origin: smoke". */
export function originLabel(origin: PaymentOrigin): string {
  return ORIGIN_LABELS[origin];
}

/** @see categoryLabel */
export function quoteStatusLabel(status: QuoteStatus): string {
  return QUOTE_STATUS_LABELS[status];
}
