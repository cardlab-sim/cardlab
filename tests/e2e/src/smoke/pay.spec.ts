import { test } from '@playwright/test';

// Stub uses test.fixme; PR-08 replaces it. Runs only in the smoke project (DEMO_URL); never calls /api/test/reset.

// T-E-7 · PR-08 · e2e
// Given the deployed demo (no reset) and test.use({ extraHTTPHeaders: { 'X-CardLab-Origin': 'deploy-smoke' } }), When a £1.00 purchase is simulated on Ops van, the fake bank is set to Complete explicitly and the full balance is paid from the cardholder view, Then the receipt shows W-2 within 3 s of pressing confirm, the balance is £0.00 and the admin record shows origin smoke and a simulated outcome
// Catches: a deploy that cannot take a payment, a slow confirm, or smoke records indistinguishable from real use (AC-26, NFR-2, DEC-14, DEC-15)
test.fixme('T-E-7 smoke on demo: pay the full balance within 3 s', async () => {});
