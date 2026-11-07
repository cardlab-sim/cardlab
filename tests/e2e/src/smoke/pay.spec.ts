import { expect, test } from '@playwright/test';
import { Api } from '../api';
import { CardholderPage } from '../pages/CardholderPage';
import { PaymentRecordPage } from '../pages/PaymentRecordPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

// Runs only in the smoke project (DEMO_URL); never calls /api/test/reset. The header marks the payment as a deploy
// smoke (DEC-15): only the quote endpoint reads it, every other call ignores it.
test.use({ extraHTTPHeaders: { 'X-CardLab-Origin': 'deploy-smoke' } });

// T-E-7 · PR-08 · e2e
// Given the deployed demo (no reset) and test.use({ extraHTTPHeaders: { 'X-CardLab-Origin': 'deploy-smoke' } }), When a £1.00 purchase is simulated on Ops van, the fake bank is set to Complete explicitly and the full balance is paid from the cardholder view, Then the receipt shows W-2 within 3 s of pressing confirm, the balance is £0.00 and the admin record shows origin smoke and a simulated outcome
// Catches: a deploy that cannot take a payment, a slow confirm, or smoke records indistinguishable from real use (AC-26, NFR-2, DEC-14, DEC-15)
test('T-E-7 smoke on demo: pay the full balance within 3 s', async ({ page, request }) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '1.00');
  await api.setBankOutcome('complete');

  const view = new CardholderPage(page);
  await view.goto(opsVan);
  await view.pay();
  const review = new ReviewPage(page);
  await review.lines();

  const started = Date.now();
  await review.confirm();
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/, { timeout: 10_000 });
  const receipt = new ReceiptPage(page);
  const message = await receipt.message();
  const elapsed = Date.now() - started;
  const reference = receipt.reference();
  expect(message).toMatch(
    new RegExp(
      `^Payment complete\\. £[\\d,]+\\.\\d{2} has left your current account and paid your card\\. Your card balance is now £0\\.00\\. Reference ${reference}\\.$`,
    ),
  );
  expect(elapsed, 'ms from confirm to the receipt').toBeLessThan(3000);

  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded();
  expect(await view.balance()).toBe('£0.00');

  const record = new PaymentRecordPage(page);
  await record.goto(opsVan, reference);
  expect(await record.origin()).toBe('origin: smoke');
  expect(await record.simulated()).toBe(true);
});
