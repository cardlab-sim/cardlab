import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-10 replaces each with a real test.

// T-E-4 · PR-10 · e2e
// Given J-1..J-3 executed (including an unknown-card processor request via api.ts), When the text and HTML of the list page and every card detail page are scanned with expectNoPan, Then no 16-digit run appears and each card shows its last four
// Catches: PAN rendered in the UI (AC-24, NFR-1)
test.fixme('T-E-4 no full card number on any screen', async () => {});
