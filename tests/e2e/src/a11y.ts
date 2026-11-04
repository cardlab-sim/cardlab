import type { Page } from '@playwright/test';
import { notImplemented } from './notImplemented';

/**
 * Runs axe (AxeBuilder with wcag2a, wcag2aa, wcag21a, wcag21aa tags) and fails listing any violation with impact
 * "critical" or "serious" (NFR-6, AC-37). Implemented in PR-10.
 */
export function expectNoSeriousViolations(page: Page): Promise<void> {
  return notImplemented('PR-10', page);
}

/**
 * Presses Tab until the focused element has the given accessible name (max 50 presses), failing otherwise; used to
 * drive journeys keyboard-only (AC-37). Implemented in PR-10.
 */
export function tabTo(page: Page, name: string | RegExp): Promise<void> {
  return notImplemented('PR-10', page, name);
}

/** Asserts the page has no horizontal scroll at the current viewport (§4.1 table rule). Implemented in PR-10. */
export function expectNoHorizontalScroll(page: Page): Promise<void> {
  return notImplemented('PR-10', page);
}
