import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-08 replaces each with a real test.

// T-E-2 · PR-08 · e2e
// Given a card owing £0.00, When the view opens, Then W-7 and no pay button
// Catches: a pay action offered for nothing (AC-5)
test.fixme('T-E-2 nothing to pay shows W-7', async () => {});

// T-E-2 · PR-08 · e2e
// Given the review screen of £150.50, When a £20.00 purchase is approved through the API and the cardholder confirms, Then W-6 with £170.50 in an alert and Review again leads to a new review of £170.50
// Catches: paying a stale amount (AC-8)
test.fixme('T-E-2 balance changed refuses with W-6', async () => {});

// T-E-2 · PR-08 · e2e
// Given the fake bank set to refuse (insufficient funds) / refuse (account unavailable) through the panel, When the cardholder pays, Then the receipt shows W-3 / W-4 with £150.50 and fee £0.00; balance still £150.50
// Catches: refusals moving money or showing the wrong words (AC-12, AC-35)
test.fixme('T-E-2 bank refusals show W-3 and W-4', async () => {});
