import { expect, test } from '@playwright/test';
import { expectNoSeriousViolations, tabTo } from '../a11y';
import { Api } from '../api';
import { CardholderPage } from '../pages/CardholderPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-5 · PR-08 · e2e
// Given the cardholder view, review and receipt, When axe runs on each and the whole journey (view → review → confirm → receipt → check status) is driven by keyboard only, Then zero critical or serious violations and the journey completes with a visible focus ring
// Catches: inaccessible payment pages (AC-28, NFR-5)
test('T-E-5 axe and keyboard-only journey', async ({ page, request }) => {
  // One real bank budget of 10 s: the receipt must be pending for the keyboard to reach Check status.
  test.setTimeout(60_000);
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '150.50');
  await api.setBankOutcome('timeout_debit_applied');
  const view = new CardholderPage(page);
  await view.goto(opsVan);
  await expectNoSeriousViolations(page);

  await tabTo(page, 'Pay full balance');
  await page.keyboard.press('Enter');
  const review = new ReviewPage(page);
  await review.lines();
  await expectNoSeriousViolations(page);

  await tabTo(page, 'Confirm payment of £150.50');
  await page.keyboard.press('Enter');
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/, { timeout: 15_000 });
  const receipt = new ReceiptPage(page);
  expect(await receipt.state()).toBe('Pending');
  await expectNoSeriousViolations(page);

  await tabTo(page, 'Check status');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { level: 1, name: 'Payment complete' })).toBeVisible();
  expect(await receipt.state()).toBe('Completed');
  expect(await receipt.message()).toContain(
    'Payment complete. £150.50 has left your current account',
  );
  await expectNoSeriousViolations(page);

  await tabTo(page, 'Back to your card');
  await page.keyboard.press('Enter');
  await view.waitUntilLoaded();
  expect(await view.balance()).toBe('£0.00');
  await expectNoSeriousViolations(page);
});
