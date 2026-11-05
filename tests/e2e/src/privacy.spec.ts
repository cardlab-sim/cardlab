import { expect, test } from '@playwright/test';
import { Api } from './api';
import { expectNoPan, findPans } from './panScanner';
import { CardDetailPage } from './pages/CardDetailPage';
import { CardListPage } from './pages/CardListPage';
import { seed } from './seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-4 · PR-10 · e2e
// Given J-1..J-3 executed (including an unknown-card processor request via api.ts), When the text and HTML of the list page and every card detail page are scanned with expectNoPan, Then no 16-digit run appears and each card shows its last four
// Catches: PAN rendered in the UI (AC-24, NFR-1)
test('T-E-4 no full card number appears in the UI or API responses, only the last four', async ({
  page,
  request,
}) => {
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);

  // J-1, J-2 and J-3 via the UI on Ops van.
  const detail = new CardDetailPage(page);
  await detail.goto(opsVan);
  await detail.freeze();
  await detail.simulate('1.00', 'Fuel', 'Shell');
  await detail.unfreeze();
  await detail.setLimit('Fuel', 'Day', '50.00');
  await detail.simulate('2.00', 'Fuel', 'Shell');

  // Processor requests, including one for an unknown card.
  const known = await api.authorise({
    cardNumber: seed.cards.opsVan.cardNumber,
    amount: '3.00',
    category: 'fuel',
    merchant: 'Shell',
    timestamp: '2026-10-24T10:00:00Z',
  });
  const unknown = await api.authorise({
    cardNumber: seed.unknownCardNumber,
    amount: '3.00',
    category: 'fuel',
    merchant: 'Shell',
    timestamp: '2026-10-24T10:00:00Z',
  });
  expect(unknown.status).toBe(404);
  expect(unknown.body.reason).toBe('card_not_found');
  expectNoPan(known.raw, 'processor response');
  expectNoPan(unknown.raw, 'unknown-card response');

  const list = new CardListPage(page);
  await list.goto();
  await expectPageHasNoPan(page, 'list page');
  for (const card of Object.values(seed.cards)) {
    await expect(page.getByRole('row').filter({ hasText: card.name })).toContainText(card.lastFour);
    await list.open(card.name);
    await expect(page.getByText(`•••• ${card.lastFour}`)).toBeVisible();
    await expectPageHasNoPan(page, `${card.name} detail page`);
    await page.goBack();
    await expect(page.getByRole('table', { name: 'Card list' })).toBeVisible();
  }
});

async function expectPageHasNoPan(
  page: import('@playwright/test').Page,
  where: string,
): Promise<void> {
  expectNoPan(await page.locator('body').innerText(), `${where} text`);
  expectNoPan(await page.content(), `${where} HTML`);
  expect(findPans(await page.content())).toEqual([]);
}
