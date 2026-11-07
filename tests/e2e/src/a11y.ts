import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

/**
 * Runs axe (AxeBuilder with wcag2a, wcag2aa, wcag21a, wcag21aa tags) and fails listing any violation with impact
 * "critical" or "serious" (NFR-6, AC-37).
 */
export async function expectNoSeriousViolations(page: Page): Promise<void> {
  // @axe-core/playwright bundles playwright-core 1.64 types while the suite pins 1.63; the runtime API is the same.
  const results = await new AxeBuilder({
    page: page as unknown as ConstructorParameters<typeof AxeBuilder>[0]['page'],
  })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  const serious = results.violations
    .filter((v) => v.impact === 'critical' || v.impact === 'serious')
    .map((v) => `${v.impact ?? ''} ${v.id}: ${v.help} (${v.nodes.length.toString()} node(s))`);
  expect(serious, `axe violations:\n${serious.join('\n')}`).toEqual([]);
}

/**
 * Presses Tab until the focused element has the given accessible name (max 50 presses), failing otherwise; used to
 * drive journeys keyboard-only (AC-37). Also asserts the focused control shows a visible focus outline of at least 2 px (§4.1).
 */
export async function tabTo(page: Page, name: string | RegExp): Promise<void> {
  for (let presses = 0; presses < 50; presses++) {
    await page.keyboard.press('Tab');
    const focused = await page.evaluate(() => {
      const el = document.activeElement;
      if (!el || el === document.body) {
        return null;
      }
      const labelled = el.getAttribute('aria-label');
      const byIds = (el.getAttribute('aria-labelledby') ?? '')
        .split(' ')
        .map((id) => document.getElementById(id)?.textContent ?? '')
        .join(' ');
      const labels = Array.from((el as HTMLInputElement).labels ?? [])
        .map((l) => l.textContent ?? '')
        .join(' ');
      const style = getComputedStyle(el);
      const ring = style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) >= 2;
      return {
        name: (labelled ?? (byIds.trim() || labels.trim() || el.textContent)).trim(),
        ring,
      };
    });
    if (
      focused !== null &&
      (typeof name === 'string' ? focused.name === name : name.test(focused.name))
    ) {
      expect(focused.ring, `focus ring visible on "${focused.name}"`).toBe(true);
      return;
    }
  }
  throw new Error(`Could not tab to "${String(name)}" within 50 presses`);
}

/** Asserts the page has no horizontal scroll at the current viewport (§4.1 table rule). */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow, 'horizontal overflow in px').toBeLessThanOrEqual(0);
}
