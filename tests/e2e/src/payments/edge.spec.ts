import { expect, test, type Page } from '@playwright/test';
import { Api } from '../api';
import { CardholderPage } from '../pages/CardholderPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-2 · PR-08 · e2e
// Given a card owing £0.00, When the view opens, Then W-7 and no pay button
// Catches: a pay action offered for nothing (AC-5)
test('T-E-2 nothing to pay shows W-7', async ({ page, request }) => {
  const opsVan = await new Api(request).cardId(seed.cards.opsVan.name);
  const view = new CardholderPage(page);

  await view.goto(opsVan);

  expect(await view.balance()).toBe('£0.00');
  await expect(view.balanceRegion).toContainText('Nothing to pay. Your card balance is £0.00.');
  await expect(page.getByRole('button', { name: 'Pay full balance' })).toHaveCount(0);
});

// T-E-2 · PR-08 · e2e
// Given the review screen of £150.50, When a £20.00 purchase is approved through the API and the cardholder confirms, Then W-6 with £170.50 in an alert and Review again leads to a new review of £170.50
// Catches: paying a stale amount (AC-8)
test('T-E-2 balance changed refuses with W-6', async ({ page, request }) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '150.50');
  const view = new CardholderPage(page);
  await view.goto(opsVan);
  await view.pay();
  const review = new ReviewPage(page);
  expect((await review.lines())[1]).toBe('Amount: £150.50');

  await api.simulatePurchase(opsVan, '20.00');
  await review.confirm();

  await expect(page.getByRole('alert')).toHaveText(
    'Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.',
  );
  const { raw } = await api.cardholderView(opsVan);
  expect(raw).toContain('"balanceOwed":"170.50"');
  expect(raw).toContain('"pendingPayment":null');

  await page.getByRole('button', { name: 'Review again' }).click();
  await expect(page.getByRole('button', { name: 'Confirm payment of £170.50' })).toBeVisible();
  expect((await review.lines())[1]).toBe('Amount: £170.50');
});

// T-E-2 · PR-08 · e2e
// Given the fake bank set to refuse (insufficient funds) / refuse (account unavailable) through the panel, When the cardholder pays, Then the receipt shows W-3 / W-4 with £150.50 and fee £0.00; balance still £150.50
// Catches: refusals moving money or showing the wrong words (AC-12, AC-35)
test('T-E-2 bank refusals show W-3 and W-4', async ({ page, request }) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '150.50');
  const view = new CardholderPage(page);
  await view.goto(opsVan);

  await view.setFakeBank('Refuse (insufficient funds)');
  const insufficient = await payAndReadReceipt(page, view);
  expect(insufficient.state).toBe('Failed');
  expect(insufficient.message).toBe(
    `Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still £150.50. Add money to the account, then try again. Reference ${insufficient.reference}.`,
  );
  expect(insufficient.fields['Amount']).toBe('£150.50');
  expect(insufficient.fields['Fee']).toBe('£0.00');
  await expect(page.getByRole('alert')).toBeVisible();

  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded();
  expect(await view.balance()).toBe('£150.50');
  expect(await view.accountBalance()).toBe('£25,000.00');
  await view.setFakeBank('Refuse (account unavailable)');
  const unavailable = await payAndReadReceipt(page, view);
  expect(unavailable.state).toBe('Failed');
  expect(unavailable.message).toBe(
    `Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference ${unavailable.reference}.`,
  );
  expect(unavailable.fields['Amount']).toBe('£150.50');
  expect(unavailable.fields['Fee']).toBe('£0.00');

  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded();
  expect(await view.balance()).toBe('£150.50');
  expect(await view.accountBalance()).toBe('£25,000.00');
  expect(await view.readAccountActivity()).toEqual([]);
});

async function payAndReadReceipt(page: Page, view: CardholderPage) {
  await view.pay();
  const review = new ReviewPage(page);
  await review.lines();
  await review.confirm();
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/);
  const receipt = new ReceiptPage(page);
  return {
    reference: receipt.reference(),
    state: await receipt.state(),
    message: await receipt.message(),
    fields: await receipt.fields(),
  };
}
