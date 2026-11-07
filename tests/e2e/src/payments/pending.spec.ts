import { expect, test } from '@playwright/test';
import { Api } from '../api';
import { CardholderPage } from '../pages/CardholderPage';
import { PaymentRecordPage } from '../pages/PaymentRecordPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-3 · PR-08 · e2e
// Given the fake bank set to time out (debit applied) through the panel, When the cardholder confirms, Then within 12 s the receipt shows W-5 and Check status; the view shows the pending notice and trying to pay shows W-9; after opening the view again (auto check) the payment is completed and the balance £0.00
// Catches: the real 10 s budget path or the auto check failing (AC-11, AC-13, AC-14, AC-34)
test('T-E-3 J-3 timeout then automatic status check', async ({ page, request }) => {
  // Two real bank budgets of 10 s each: the timed-out debit and one unanswered automatic check.
  test.setTimeout(90_000);
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '150.50');
  const view = new CardholderPage(page);
  await view.goto(opsVan);
  await view.setFakeBank('Time out, debit applied');
  await view.pay();
  const review = new ReviewPage(page);
  await review.lines();

  const started = Date.now();
  await review.confirm();
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/, { timeout: 15_000 });
  const receipt = new ReceiptPage(page);
  expect(await receipt.state()).toBe('Pending');
  expect(Date.now() - started, 'ms from confirm to the pending receipt').toBeLessThanOrEqual(
    12_000,
  );
  const reference = receipt.reference();
  expect(await receipt.message()).toBe(
    `Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference ${reference}.`,
  );
  expect((await receipt.fields())['Fee']).toBe('£0.00');
  await expect(page.getByRole('button', { name: 'Check status' })).toHaveCount(1);

  // The bank will not answer the next status check, so the automatic check on opening the view leaves it pending.
  await api.setStatusCheck('does_not_answer');
  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded(20_000);
  await expect(view.pendingRegion).toContainText(`Reference ${reference}`);
  await expect(view.pendingRegion.getByRole('button', { name: 'Check status' })).toBeVisible();
  await expect(view.balanceRegion).toContainText(
    `A payment of £150.50 (reference ${reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.`,
  );
  await expect(page.getByRole('button', { name: 'Pay full balance' })).toHaveCount(0);
  expect(await view.balance()).toBe('£150.50');

  // Trying to pay anyway is refused with W-9.
  await page.goto(`/cardholder/${opsVan}/pay`);
  await expect(page.getByRole('alert')).toHaveText(
    `A payment of £150.50 (reference ${reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.`,
  );

  // Opening the view again: the automatic check is answered and finds the debit the bank applied.
  await view.goto(opsVan);
  await expect(view.pendingRegion).toHaveCount(0);
  expect(await view.balance()).toBe('£0.00');
  expect(await view.accountBalance()).toBe('£24,849.50');
  const rows = (await view.readActivity()).filter((row) => row.description.includes(reference));
  expect(rows).toHaveLength(1);
  expect(rows[0]?.state).toBe('Completed');
  expect(
    (await view.readAccountActivity()).filter((row) => row.reference === reference),
  ).toHaveLength(1);

  // AC-34: the payment's effective time is the bank's reported debit time.
  const record = new PaymentRecordPage(page);
  await record.goto(opsVan, reference);
  const payment = await record.section('Payment');
  const effective = /Effective time\s+([^\n]+)/.exec(payment)?.[1]?.trim();
  const debited = /Bank debit time\s+([^\n]+)/.exec(payment)?.[1]?.trim();
  expect(debited, 'bank debit time shown').toBeTruthy();
  expect(effective).toBe(debited);
});
