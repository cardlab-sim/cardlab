import { describe, test } from 'vitest';

describe('FakeBankPanel', () => {
  // T-W-5 · PR-03 · web unit
  // Given getFakeBank returning complete/answers, When rendered, Then a region named "Fake bank (simulation control)" with labelled selects "Next debit outcome" (five options in R-15 order) and "Next status check", the current setting as text; choosing "Refuse (insufficient funds)" and Set calls setFakeBank({nextDebitOutcome: 'refuse_insufficient_funds', nextStatusCheck: 'answers'}) and shows the new setting
  // Catches: an unlabelled or unusable steering control (R-15, AC-28, CQ-8)
  test.todo('T-W-5 sets the next outcome through labelled controls');
});
