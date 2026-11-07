import { describe, test } from 'vitest';

describe('CardholderViewPage', () => {
  // T-W-2 · PR-03 · web unit
  // Given FakeApiClient with Ops van owing £150.50, When rendered at /cardholder/:id, Then heading "Ops van" with "•••• 0001", balance "£150.50", a "Pay full balance" link to /cardholder/:id/pay, the linked account name, "ending 7731", available balance and authority text, both activity tables, and the Fake bank section last in DOM order
  // Catches: missing balance/account or the fake-bank control placed above the pay action (AC-4, R-15, CQ-8)
  test.todo('T-W-2 shows balance, account, activity and the fake bank section last');

  // T-W-2 · PR-03 · web unit
  // Given balance £0.00 / £1,000,000.01, When rendered, Then no pay button and the server's availabilityMessage (W-7 / W-8) verbatim
  // Catches: a pay button offered when nothing can be paid (AC-5, AC-24)
  test.todo('T-W-2 shows W-7 or W-8 instead of the pay action');

  // T-W-2 · PR-03 · web unit
  // Given a pending payment in the view, When rendered, Then checkPayment is called once for its reference and the view re-read; W-5 shows in a role="status" Notice with a "Check status" button; availability payment_pending shows W-9 with "Check status"
  // Catches: no automatic status check on open, or a pay button while pending (AC-11, AC-13, AC-34, DEC-13)
  test.todo('T-W-2 auto-checks a pending payment and offers Check status');

  // T-W-2 · PR-03 · web unit
  // Given getCardholderView rejecting with ApiError 404, When rendered, Then Notice "Card not found"
  // Catches: blank page for unknown cards (design §9)
  test.todo('T-W-2 shows card not found');
});
