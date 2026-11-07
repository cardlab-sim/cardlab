import type { ActivityEntry, PaymentListItem } from '../../api/types';
import { notImplemented } from '../../notImplemented';

/**
 * 002 "Activity" (replaces 001's Transactions table on the card detail, DEC-12): DataTable with caption
 * "Transaction history" (kept so 001 page objects and AC-22/AC-38 still read it), newest first. Columns: Time (London),
 * Type (badge Purchase/Payment), Description (merchant, or a link "Payment PAY-…" to `/cards/:id/payments/:reference`),
 * Category (purchases; "—" for payments), Amount, Outcome (badge: Approved/Declined, or Pending/Completed/Failed),
 * Reason (purchase reason label; "origin: smoke" badge for smoke payments). Implemented in PR-07.
 * @param props.cardId - Card id (for record links).
 * @param props.entries - Rows from `listActivity`.
 */
export function ActivitySection(props: { cardId: string; entries: ActivityEntry[] }): never {
  return notImplemented('PR-07', props);
}

/**
 * 002 "Payments": every reference newest first (incl. open and refused), each with reference link, amount, created
 * time, status and state badges, origin badge (DEC-15) and failure reason. Implemented in PR-07.
 * @param props.cardId - Card id (for record links).
 * @param props.payments - Items from `listPaymentRecords`.
 */
export function PaymentsSection(props: { cardId: string; payments: PaymentListItem[] }): never {
  return notImplemented('PR-07', props);
}
