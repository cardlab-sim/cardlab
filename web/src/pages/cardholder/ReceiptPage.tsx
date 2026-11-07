import { notImplemented } from '../../notImplemented';

/**
 * Route `/cardholder/:id/payments/:reference` (002 R-12, §4.1). Uses the receipt from navigation state, else
 * `getReceipt(id, reference)`. Heading by state: "Payment complete" / "Payment not made" / "Payment pending"; the
 * receipt `message` verbatim in a Notice (role="status" for pending/completed, role="alert" for failed); a `<dl>`
 * with Reference, Amount, Fee, Time (Europe/London), From, To, Outcome (text badge; plus a "Not received" reason badge
 * for not_received); a "Check status" button when pending (calls `checkPayment`, shows the new message, W-10 when
 * unanswered); link "Back to your card". Never renders the fake-bank control. Implemented in PR-03.
 */
export function ReceiptPage(): never {
  return notImplemented('PR-03');
}
