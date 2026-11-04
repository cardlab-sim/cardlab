import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-10 replaces each with a real test.

// T-E-7 · PR-10 · e2e
// Given the deployed demo (no reset), When the admin opens Ops van, freezes, simulates £1.00 Fuel (declined card_frozen), unfreezes and simulates £1.00 Fuel (approved), Then each simulate result appears within 2 s of submit and the card ends active
// Catches: a deploy that serves but cannot decide, or a slow round trip (AC-36, G-1); leaving demo frozen
test.fixme('T-E-7 smoke on demo: J-1 on Ops van within 2 s, restoring state', async () => {});
