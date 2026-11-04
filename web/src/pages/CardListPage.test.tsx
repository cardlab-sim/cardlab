import { describe, test } from 'vitest';

describe('CardListPage', () => {
  // T-W-7 · PR-02 · web unit
  // Given FakeApiClient with Ops van (active) and Marketing (frozen), When rendered at /, Then a table row per card with name, cardholder, "0001", a status badge with the text "Active"/"Frozen", and a link "Open Ops van" to /cards/{id}
  // Catches: status shown by colour only; missing navigation (R-8, §4.1)
  test.todo('T-W-7 lists cards with text status badges and open links');

  // T-W-7 · PR-02 · web unit
  // Given listCards rejecting with ApiError 503, When rendered, Then an error Notice (role="alert") and no table
  // Catches: blank page on API failure (design §9)
  test.todo('T-W-7 shows an error notice when the list fails');
});
