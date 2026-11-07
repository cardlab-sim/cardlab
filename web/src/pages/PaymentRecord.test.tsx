import { describe, test } from 'vitest';

describe('PaymentRecordPage', () => {
  // T-W-6 · PR-07 · web unit
  // Given a completed simulated record, When rendered at /cards/:id/payments/:reference, Then "What the cardholder saw" lists the wording heading, the six lines verbatim, the button text and "Wording version v1"; Acceptance shows time (London), "Priya Nair" (cardholder), authority basis and origin; Payment shows a Completed badge, £150.50, fee £0.00, balance before/after, effective time and "Simulated outcome"; the events table lists each event with time, kind, from → to and detail
  // Catches: an admin record missing any AC-20 element (AC-20, R-14)
  test.todo('T-W-6 shows what was shown, accepted and the outcome');

  // T-W-6 · PR-07 · web unit
  // Given a refused reference / a pending payment / a smoke-origin record / a 404, When rendered, Then refusal reason and balance at refusal / a Pending badge and no balances / an "origin: smoke" badge / Notice "Payment not found" with a link back to the card
  // Catches: states other than completed rendered wrongly (AC-20, DEC-15)
  test.todo('T-W-6 renders refused, pending, smoke and missing records');
});
