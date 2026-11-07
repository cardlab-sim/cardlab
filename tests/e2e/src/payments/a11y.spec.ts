import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-08 replaces each with a real test.

// T-E-5 · PR-08 · e2e
// Given the cardholder view, review and receipt, When axe runs on each and the whole journey (view → review → confirm → receipt → check status) is driven by keyboard only, Then zero critical or serious violations and the journey completes with a visible focus ring
// Catches: inaccessible payment pages (AC-28, NFR-5)
test.fixme('T-E-5 axe and keyboard-only journey', async () => {});
