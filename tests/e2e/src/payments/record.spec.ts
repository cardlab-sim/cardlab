import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-08 replaces each with a real test.

// T-E-6 · PR-08 · e2e
// Given a completed, a failed, a pending and a refused reference on Ops van, When the admin opens /cards/:id, Then the heading shows the balance owed, Activity lists payments as payments and the Payments section lists all four; opening each record shows the shown wording with version v1, acceptance, outcome and events
// Catches: the admin unable to audit what was shown and accepted (AC-20, AC-2)
test.fixme('T-E-6 admin sees balance, activity and each payment record', async () => {});
