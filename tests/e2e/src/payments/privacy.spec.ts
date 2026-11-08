import { expect, test } from '@playwright/test';
import { Api } from '../api';
import { expectNoFullNumbers } from '../panScanner';
import { CardholderPage } from '../pages/CardholderPage';
import { PaymentRecordPage } from '../pages/PaymentRecordPage';
import { ReceiptPage } from '../pages/ReceiptPage';
import { ReviewPage } from '../pages/ReviewPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-4 · PR-08 · e2e
// Given J-1 with the fake bank set to refuse, When the review and receipt pages are shown and every page text and API response of the journey is scanned, Then neither page contains the fake-bank region, no full card number or "51907731" appears anywhere, and the admin record of the failed payment says simulated
// Catches: the steering control on the one-screen evidence, or numbers leaking (AC-23, AC-33, NFR-3)
test('T-E-4 control placement and no full numbers', async ({ page, request }) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  await api.simulatePurchase(opsVan, '150.50');
  const apiBodies: { where: string; text: string }[] = [];
  const reads: Promise<void>[] = [];
  page.on('response', (response) => {
    const url = new URL(response.url());
    if (url.pathname.startsWith('/api/')) {
      reads.push(
        response.text().then(
          (text) => {
            apiBodies.push({ where: `${response.request().method()} ${url.pathname}`, text });
          },
          () => undefined,
        ),
      );
    }
  });
  const pageTexts: { where: string; text: string }[] = [];
  const capture = async (where: string): Promise<void> => {
    pageTexts.push({ where: `${where} text`, text: await page.locator('body').innerText() });
    pageTexts.push({ where: `${where} html`, text: await page.content() });
  };

  const view = new CardholderPage(page);
  await view.goto(opsVan);
  await view.setFakeBank('Refuse (insufficient funds)');
  await expect(view.fakeBankRegion).toBeVisible();
  await capture('cardholder view');

  await view.pay();
  const review = new ReviewPage(page);
  await review.lines();
  await expect(page.getByRole('region', { name: 'Fake bank (simulation control)' })).toHaveCount(0);
  await expect(page.getByLabel('Next debit outcome')).toHaveCount(0);
  await capture('review');

  await review.confirm();
  await page.waitForURL(/\/cardholder\/[^/]+\/payments\/PAY-/);
  const receipt = new ReceiptPage(page);
  expect(await receipt.state()).toBe('Failed');
  await expect(page.getByRole('region', { name: 'Fake bank (simulation control)' })).toHaveCount(0);
  await expect(page.getByLabel('Next debit outcome')).toHaveCount(0);
  await capture('receipt');
  const reference = receipt.reference();

  await page.getByRole('link', { name: 'Back to your card' }).click();
  await view.waitUntilLoaded();
  await capture('cardholder view after payment');

  const record = new PaymentRecordPage(page);
  await record.goto(opsVan, reference);
  expect(await record.simulated()).toBe(true);
  await capture('admin record');
  await page.goto(`/cards/${opsVan}`);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await capture('admin card');

  await page.waitForLoadState('networkidle');
  await Promise.all(reads);
  const seen = apiBodies.map((b) => b.where);
  expect(seen).toContain(`POST /api/cardholder/cards/${opsVan}/quotes`);
  expect(
    seen.some((w) => /^POST \/api\/cardholder\/cards\/[^/]+\/payments\/[^/]+\/confirm$/.test(w)),
  ).toBe(true);
  expect(pageTexts.length, 'page texts captured').toBeGreaterThan(5);
  for (const { where, text } of pageTexts) {
    expectNoFullNumbers(text, where);
  }
  const own = await api.cardholderView(opsVan);
  expectNoFullNumbers(own.raw, 'GET cardholder view');
  expect(apiBodies.length, 'API responses captured').toBeGreaterThan(5);
  for (const { where, text } of apiBodies) {
    expectNoFullNumbers(text, `API response ${where}`);
  }
  expect(own.raw).toContain(`"lastFour":"${seed.linkedAccount.lastFour}"`);
  expect(own.raw).toContain(`"lastFour":"${seed.cards.opsVan.lastFour}"`);
});
