import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-08 replaces each with a real test.

// T-E-4 · PR-08 · e2e
// Given J-1 with the fake bank set to refuse, When the review and receipt pages are shown and every page text and API response of the journey is scanned, Then neither page contains the fake-bank region, no full card number or "51907731" appears anywhere, and the admin record of the failed payment says simulated
// Catches: the steering control on the one-screen evidence, or numbers leaking (AC-23, AC-33, NFR-3)
test.fixme('T-E-4 control placement and no full numbers', async () => {});
