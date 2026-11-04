import type { AuditEntry, CardDetail, Decision, Limit, Transaction } from '../../api/types';
import { notImplemented } from '../../notImplemented';

/** Freeze/Unfreeze button (label by status) with pending state and an inline Notice on error. Implemented in PR-07. */
export function StatusSection(props: { card: CardDetail; onChanged: () => void }) {
  return notImplemented('PR-07', props);
}

/**
 * "Spend limits": DataTable (Category, Period, Amount £, Remove button per row named "Remove Fuel day limit") and an
 * inline form (category select, period select, amount input, Save). Saving an existing (category, period) replaces it.
 * Invalid amount → inline field error from the 400 ProblemDetails (or client-side check for empty/non-numeric).
 * Implemented in PR-07.
 */
export function LimitsSection(props: { cardId: string; limits: Limit[]; onChanged: () => void }) {
  return notImplemented('PR-07', props);
}

/** "Audit": ordered list newest first: action label, actor, London time, old → new value. Implemented in PR-07. */
export function AuditSection(props: { entries: AuditEntry[] }) {
  return notImplemented('PR-07', props);
}

/**
 * "Simulate purchase": amount, category select, merchant (all labelled), Submit with pending state; result Notice shows
 * outcome badge, the reason code (e.g. `limit_exceeded`) and remaining per limit; `problems` shown inline per field.
 * Implemented in PR-07.
 */
export function SimulateSection(props: {
  cardId: string;
  onDecided: (decision: Decision) => void;
}) {
  return notImplemented('PR-07', props);
}

/** "Transactions": DataTable newest first: Time (London), Merchant, Category, Amount, Outcome badge, Reason. Implemented in PR-07. */
export function TransactionsSection(props: { transactions: Transaction[] }) {
  return notImplemented('PR-07', props);
}
