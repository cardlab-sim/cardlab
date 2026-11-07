import { notImplemented } from '../notImplemented';

/**
 * Route `/cards/:id/payments/:reference`: the admin's read-only payment record (002 R-14, AC-20, §4.1). Loads
 * `getPaymentRecord(id, reference)`. Sections: "What the cardholder saw" (wording heading, lines verbatim, button text,
 * "Wording version v1"); "Acceptance" (time, actor name and kind, authority basis, origin badge); "Payment" (state badge,
 * amount, fee, balances before/after, effective time, settled time, bank debit time, "Simulated outcome" text when
 * simulated, failure reason) or the refusal (reason, balance at refusal); "Events" table (time, kind, from → to, detail,
 * simulated). Times Europe/London; amounts £1,234.56. 404 → Notice "Payment not found" with a link back to the card.
 * Implemented in PR-07.
 */
export function PaymentRecordPage(): never {
  return notImplemented('PR-07');
}
