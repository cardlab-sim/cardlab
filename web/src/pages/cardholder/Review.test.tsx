import { describe, test } from 'vitest';

describe('ReviewPage', () => {
  // T-W-3 · PR-03 · web unit
  // Given createQuote returning the W-1 wording for £150.50, When rendered at /cardholder/:id/pay, Then exactly one quote is created (also under StrictMode), the heading and the six lines appear in order inside one main region with the button "Confirm payment of £150.50", and there is no fake-bank control
  // Catches: two references per visit, or fee/execution time not on the same screen as the button (AC-6, AC-33, OB-7)
  test.todo('T-W-3 renders W-1 and the confirm button on one screen');

  // T-W-3 · PR-03 · web unit
  // Given confirmPayment held by FakeApiClient.hold(), When the button is clicked twice and Enter is pressed, Then confirmPayment was called once, the button shows a pending state with aria-disabled="true" and keeps focus; on resolve the app navigates to /cardholder/:id/payments/:reference
  // Catches: double submit or focus loss while confirming (AC-9, 001 BUG-010)
  test.todo('T-W-3 guards the confirm button while pending');

  // T-W-3 · PR-03 · web unit
  // Given confirmPayment rejecting with 409 balance_changed (message W-6), When confirm, Then the W-6 message verbatim in a role="alert" Notice and a "Review again" button that creates a new quote; given createQuote rejecting 409 nothing_to_pay, Then its message and a Back link
  // Catches: refusals shown without the next step, or client-made wording (AC-8, AC-30, DEC-10)
  test.todo('T-W-3 shows refusals verbatim with the next-step control');
});
