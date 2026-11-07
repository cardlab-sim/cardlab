import { notImplemented } from '../../notImplemented';

/**
 * Route `/cardholder/:id` (002 §4.1, DEC-11, DEC-13). Loads `getCardholderView(id)`; when `pendingPayment` is set it
 * calls `checkPayment(id, reference)` once and then re-reads the view (auto status check). Sections in DOM order:
 * heading (card name, "•••• 0001"); **Balance** (32 px figure; the primary button "Pay full balance" linking to
 * `/cardholder/:id/pay` when availability is `available`, else `availabilityMessage` verbatim, and for
 * `payment_pending` a "Check status" button); **Pending payment** Notice (W-5 message, role="status", "Check status"
 * button); **Linked current account** (display name, "ending 7731", available balance, authority sentence); **Recent
 * card activity** table (date, description, amount, type badge, state/outcome badge; ≤ 10); **Current account recent
 * activity** table (date, reference, amount; ≤ 10); last, `<FakeBankPanel />`. Unknown card → Notice "Card not found".
 * Implemented in PR-03.
 */
export function CardholderViewPage(): never {
  return notImplemented('PR-03');
}
