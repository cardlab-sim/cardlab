import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-10 replaces each with a real test.

// T-E-1 · PR-10 · e2e
// Given a reset system, When the admin opens Ops van from the list, freezes it, simulates £1.00 Fuel, unfreezes and simulates again, Then the badge reads Frozen, the first purchase shows declined card_frozen, the second approved, and the audit list shows unfreeze then freeze by Sam Ellis
// Catches: the deployed UI not wiring freeze to the decision path (AC-1, AC-3, AC-4, AC-5, G-1)
test.fixme('T-E-1 J-1 freeze and unfreeze a card', async () => {});
