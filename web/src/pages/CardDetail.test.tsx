import { describe, test } from 'vitest';

describe('CardDetailPage', () => {
  // T-W-3 · PR-07 · web unit
  // Given the limit form, When Save with amount "10.005" (FakeApiClient rejects 400 errors.amount), Then an inline error under Amount (role="alert", aria-describedby on the input) and the limits table unchanged
  // Catches: toast-only or missing validation feedback (AC-9, §4.1)
  test.todo('T-W-3 limit form shows inline errors from the API');

  // T-W-3 · PR-07 · web unit
  // Given the simulate form and fake.hold(), When Submit, Then the button shows its pending label and is disabled; after release a Notice shows outcome "Declined", the code limit_exceeded and remaining £0.00, and the new transaction is the first table row
  // Catches: no pending state, result not shown, history not refreshed (AC-22, §4.1)
  test.todo('T-W-3 simulate shows pending state then the decision and refreshes history');

  // T-W-3 · PR-07 · web unit
  // Given nextDecision invalid_request with problems [{field:"amount"}], When Submit, Then the error is inline under Amount, no transaction row added
  // Catches: problems list ignored (DEC-14)
  test.todo('T-W-3 simulate shows validation problems inline');

  // T-W-8 · PR-07 · web unit
  // Given an active card, When Freeze is pressed, Then status badge "Frozen", button now "Unfreeze", and the audit list's first item is the freeze entry with actor "Sam Ellis" and London time
  // Catches: stale status/audit after an action (AC-1, AC-23)
  test.todo('T-W-8 freeze updates status, button and audit');

  // T-W-9 · PR-07 · web unit
  // Given a card with a £1,234.56 Fuel/Day limit and a transaction at 2026-10-24T23:30:00Z, When rendered, Then the limit shows "£1,234.56" and the transaction time "25 Oct 2026 00:30"; Remove "Remove Fuel day limit" removes the row
  // Catches: raw amounts/UTC times on the page (AC-38), missing remove (AC-8)
  test.todo('T-W-9 renders limits and transactions with UK formatting and removes a limit');

  // T-W-10 · PR-07 · web unit
  // Given getCard rejecting with ApiError 404, When rendered at /cards/unknown, Then a "Card not found" notice with a link back to the card list
  // Catches: crash or blank page for unknown ids (design §9)
  test.todo('T-W-10 unknown card shows a not-found notice');
});
