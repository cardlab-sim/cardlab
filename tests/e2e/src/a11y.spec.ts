import { test } from '@playwright/test';

// Stubs use test.fixme (Playwright has no test.todo); PR-10 replaces each with a real test.

// T-E-5 · PR-10 · e2e
// Given the list page and Ops van's detail page (also with an inline limit error and a decision notice visible), When axe runs, Then zero critical or serious violations; at 1024 and 1440 px wide there is no horizontal scroll
// Catches: contrast, label or ARIA regressions (AC-37, NFR-6)
test.fixme('T-E-5 axe finds no critical or serious violations on both pages, including after errors are shown', async () => {});

// T-E-5 · PR-10 · e2e
// Given a reset system, When J-1 (open, freeze, unfreeze) and J-2 (set and remove a limit) are driven only with tabTo/Enter/typing, Then both journeys complete and the focus ring is visible on each focused control
// Catches: mouse-only controls or hidden focus (AC-37)
test.fixme('T-E-5 J-1 and J-2 complete with the keyboard only', async () => {});
