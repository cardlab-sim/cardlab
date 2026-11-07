import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-08 replaces each with a real test.

// T-E-1 · PR-08 · e2e
// Given a reset and Ops van purchases £120.00 and £30.50 (api.simulatePurchase), When the cardholder opens the view from the card list link, chooses Pay full balance and confirms, Then the view shows Priya Nair, the account ending 7731 and £25,000.00; the review shows exactly the W-1 lines and the confirm button in one main with no fake-bank control; the button guards a double click; the receipt shows W-2, fee £0.00 and a London time; back on the view the balance is £0.00, the account £24,849.50 and both activity tables list the payment with the same reference
// Catches: the deployed journey not moving money on both sides (AC-4, AC-6, AC-7, G-1)
test.fixme('T-E-1 J-1 pay the full balance end to end', async () => {});
