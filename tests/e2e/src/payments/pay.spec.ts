import { expect, test } from '@playwright/test';
import { Api } from '../api';
import { CardholderPage } from '../pages/CardholderPage';
import { CardListPage } from '../pages/CardListPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-1 · PR-08 · e2e
// Given a reset and Ops van purchases £120.00 and £30.50 (api.simulatePurchase), When the cardholder opens the view from the card list link, chooses Pay full balance and confirms, Then the view shows Priya Nair, the account ending 7731 and £25,000.00; the review shows exactly the W-1 lines and the confirm button in one main with no fake-bank control; the button guards a double click; the receipt shows W-2, fee £0.00 and a London time; back on the view the balance is £0.00, the account £24,849.50 and both activity tables list the payment with the same reference
// Catches: the deployed journey not moving money on both sides (AC-4, AC-6, AC-7, G-1)
test('T-E-1 J-1 pay the full balance end to end', async ({ page, request }) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '120.00');
  await api.simulatePurchase(opsVan, '30.50');

  await new CardListPage(page).goto();
  await page.getByRole('link', { name: `Cardholder view for ${seed.cards.opsVan.name}` }).click();
  const view = new CardholderPage(page);
  await view.waitUntilLoaded();
  await expect(page.getByRole('heading', { level: 1 })).toContainText(seed.cards.opsVan.name);
  await expect(page.getByRole('banner')).toContainText(seed.cards.opsVan.cardholderName);
  await expect(page.getByRole('banner')).not.toContainText(seed.adminName);
  await expect(view.accountRegion).toContainText(seed.linkedAccount.displayName);
  await expect(view.accountRegion).toContainText(`ending ${seed.linkedAccount.lastFour}`);
  expect(await view.accountBalance()).toBe('£25,000.00');
  expect(await view.balance()).toBe('£150.50');

  await view.pay();
  const review = new ReviewPage(page);
  expect(await review.lines()).toEqual([
    'Pay your full balance',
    'Amount: £150.50',
    'Fee: £0.00 (no fee)',
    'Total to pay: £150.50',
    'When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.',
    'From: Brightline Supplies Ltd current account, ending 7731',
    'To: Ops van card, ending 0001',
  ]);
  const confirm = page.getByRole('button', { name: 'Confirm payment of £150.50' });
  await expect(page.getByRole('main')).toHaveCount(1);
  await expect(
    page.getByRole('main').getByRole('button', { name: /^Confirm payment/ }),
  ).toBeVisible();
  await expect(confirm).toBeVisible();
  await expect(page.getByRole('region', { name: 'Fake bank (simulation control)' })).toHaveCount(0);

  const confirms: string[] = [];
  page.on('request', (r) => {
    if (r.method() === 'POST' && r.url().endsWith('/confirm')) {
      confirms.push(r.url());
    }
  });
  await confirm.dblclick();
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/);
  expect(confirms, 'confirm requests sent by a double click').toHaveLength(1);
  const receipt = new ReceiptPage(page);
  const reference = receipt.reference();
  const fields = await receipt.fields();
  expect(await receipt.state()).toBe('Completed');
  expect(await receipt.message()).toBe(
    `Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference ${reference}.`,
  );
  expect(fields['Reference']).toBe(reference);
  expect(fields['Amount']).toBe('£150.50');
  expect(fields['Fee']).toBe('£0.00');
  expect(fields['Time']).toMatch(/^\d{1,2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}$/);
  expect(fields['From']).toBe('Brightline Supplies Ltd current account, ending 7731');
  expect(fields['To']).toBe('Ops van card, ending 0001');

  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded();
  expect(await view.balance()).toBe('£0.00');
  expect(await view.accountBalance()).toBe('£24,849.50');
  const cardRows = (await view.readActivity()).filter((row) => row.description.includes(reference));
  expect(cardRows).toHaveLength(1);
  expect(cardRows[0]?.amount).toBe('£150.50');
  expect(cardRows[0]?.state).toBe('Completed');
  const accountRows = (await view.readAccountActivity()).filter(
    (row) => row.reference === reference,
  );
  expect(accountRows).toHaveLength(1);
  expect(accountRows[0]?.amount).toBe('£150.50');
});
