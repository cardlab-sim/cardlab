import { describe, test } from 'vitest';

describe('ReceiptPage', () => {
  // T-W-4 · PR-03 · web unit
  // Given a completed / failed (insufficient_funds) / failed (not_received) / pending receipt, When rendered, Then heading "Payment complete" / "Payment not made" / "Payment not made" / "Payment pending", the message verbatim (role="status" for completed and pending, role="alert" for failed), Reference, Amount, Fee £0.00, Time in Europe/London, From, To, an outcome text badge (plus "Not received" for not_received), and no fake-bank control
  // Catches: outcome shown by colour only, a missing fee, or the control on the receipt (AC-7, AC-12, AC-13, AC-33, AC-35, R-12)
  test.todo('T-W-4 renders each outcome with its message and fields');

  // T-W-4 · PR-03 · web unit
  // Given a pending receipt and checkPayment returning the W-10 message, When "Check status" is clicked, Then the new message replaces the old and the button stays available
  // Catches: no way to re-check an unanswered payment (AC-16)
  test.todo('T-W-4 checks status again from the receipt');

  // T-W-4 · PR-03 · web unit
  // Given no navigation state, When rendered, Then getReceipt(id, reference) is loaded; a 404 shows "This payment reference was not found"
  // Catches: receipt page broken on reload (design §7, §9)
  test.todo('T-W-4 reloads the receipt from the API');
});
