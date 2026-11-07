import { notImplemented } from '../../notImplemented';

/**
 * Route `/cardholder/:id/pay`: the one review screen (002 R-4, AC-6, OB-7/OB-8; §4.1). On mount calls `createQuote(id)`
 * once (guarded against the StrictMode double effect; PLANNER-DECISION: each visit mints a new reference). Renders, in one
 * `<main>` region with nothing else: heading = `wording.heading`; a `<dl>` with `wording.lines` in order; the primary
 * button labelled `wording.button` (while confirming: pending state with `aria-disabled="true"` and a click guard, focus
 * stays, 001 BUG-010); a "Back" link to `/cardholder/:id`. Confirm → `confirmPayment(id, reference)` → navigate to
 * `/cardholder/:id/payments/:reference` with the receipt in navigation state. 409 on quote → the problem `message` in
 * a Notice (role="alert") with "Back"; 409 balance_changed / quote_refused on confirm → the `message` (W-6) in a
 * Notice (role="alert") plus a "Review again" button that mints a new quote; payment_pending → W-9 Notice with a link
 * to the card view. Never renders the fake-bank control. Implemented in PR-03.
 */
export function ReviewPage(): never {
  return notImplemented('PR-03');
}
