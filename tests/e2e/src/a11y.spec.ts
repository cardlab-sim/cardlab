import { expect, test, type Locator, type Page } from '@playwright/test';
import { expectNoHorizontalScroll, expectNoSeriousViolations, tabTo } from './a11y';
import { Api } from './api';
import { CardDetailPage } from './pages/CardDetailPage';
import { CardListPage } from './pages/CardListPage';
import { seed } from './seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-5 · PR-10 · e2e
// Given the list page and Ops van's detail page (also with an inline limit error and a decision notice visible), When axe runs, Then zero critical or serious violations; at 1024 and 1440 px wide there is no horizontal scroll
// Catches: contrast, label or ARIA regressions (AC-37, NFR-6)
test('T-E-5 axe finds no critical or serious violations on both pages, including after errors are shown', async ({
  page,
  request,
}) => {
  const api = new Api(request);
  const list = new CardListPage(page);
  const detail = new CardDetailPage(page);
  const opsVan = await api.cardId(seed.cards.opsVan.name);

  for (const width of [1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await list.goto();
    await expectNoSeriousViolations(page);
    await expectNoHorizontalScroll(page);

    await detail.goto(opsVan);
    await expectNoSeriousViolations(page);
    await expectNoHorizontalScroll(page);

    await detail.setLimit('Fuel', 'Day', '10.005');
    expect(await detail.fieldError('Amount')).not.toBeNull();
    await detail.simulate('1.00', 'Fuel', 'Shell');
    await expectNoSeriousViolations(page);
    await expectNoHorizontalScroll(page);
  }
});

/** Presses Shift+Tab until the given control has focus (max 10 presses), as a keyboard user goes back to an earlier control. */
async function shiftTabTo(page: Page, control: Locator): Promise<void> {
  for (let presses = 0; presses < 10; presses++) {
    await page.keyboard.press('Shift+Tab');
    if (await control.evaluate((el) => el === document.activeElement)) {
      return;
    }
  }
  throw new Error('Could not shift-tab back to the control within 10 presses');
}

// T-E-5 · PR-10 · e2e
// Given a reset system, When J-1 (open, freeze, unfreeze), J-2 (set and remove a limit) and J-3 (simulate purchase) are driven only with tabTo/Shift+Tab/Enter/typing, Then actions keep focus (the pressed control, or the notice when it disappears) and all journeys complete and the focus ring is visible on each focused control
// Catches: mouse-only controls or hidden focus (AC-37)
test('T-E-5 J-1, J-2 and J-3 complete with the keyboard only', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('table', { name: 'Card list' })).toBeVisible();

  await tabTo(page, 'Open Ops van');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { level: 1, name: 'Ops van' })).toBeVisible();

  await tabTo(page, 'Freeze');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Unfreeze', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Freeze', exact: true })).toBeFocused();

  const limits = page.getByRole('region', { name: 'Spend limits' });
  await tabTo(page, 'Category');
  await page.keyboard.press('ArrowDown');
  await tabTo(page, 'Period');
  await tabTo(page, 'Amount');
  await page.keyboard.type('25.00');
  await tabTo(page, 'Save');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Save' })).toBeFocused();
  await expect(limits.getByRole('status').filter({ hasText: 'Limit saved' })).toBeVisible();
  await expect(
    limits
      .getByRole('table', { name: 'Limits' })
      .getByRole('row')
      .filter({ has: page.getByRole('cell') }),
  ).toHaveCount(1);

  await shiftTabTo(page, page.getByRole('button', { name: /^Remove .* limit$/ }));
  await page.keyboard.press('Enter');
  await expect(limits.locator('[tabindex="-1"]')).toBeFocused();
  await expect(limits.getByRole('status').filter({ hasText: 'Limit removed' })).toBeVisible();
  await expect(limits.getByText('No spend limits are set.')).toBeVisible();

  const simulate = page.getByRole('region', { name: 'Simulate purchase' });
  await tabTo(page, 'Amount'); // the limits form's field
  await tabTo(page, 'Amount'); // the simulate form's field
  await page.keyboard.type('1.00');
  await tabTo(page, 'Category');
  await page.keyboard.press('ArrowDown');
  await tabTo(page, 'Merchant');
  await page.keyboard.type('Shell');
  await tabTo(page, /^Submit/);
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: /^Submit/ })).toBeFocused();
  await expect(simulate.getByRole('status').filter({ hasText: 'Approved' })).toBeVisible();
  await expect(
    page
      .getByRole('table', { name: 'Transaction history' })
      .getByRole('row')
      .filter({ has: page.getByRole('cell') })
      .first(),
  ).toContainText('Shell');
  await expectNoSeriousViolations(page);
});
