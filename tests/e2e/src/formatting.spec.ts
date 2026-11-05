import { expect, test } from '@playwright/test';
import { expectNoSeriousViolations } from './a11y';
import { Api } from './api';
import { CardDetailPage } from './pages/CardDetailPage';
import { seed } from './seed';

test.use({ timezoneId: 'UTC' });

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-6 · PR-10 · e2e
// Given Ops van with a Fuel/Month £1,234.56 limit and a processor transaction at 2026-10-24T23:30:00Z (via api.ts), When the detail page renders in a browser whose time zone is UTC, Then the limit shows £1,234.56 and the transaction time 25 Oct 2026 00:30
// Catches: browser-zone dependent display (AC-38)
test('T-E-6 limit and transaction time are shown in UK format whatever the browser zone', async ({
  page,
  request,
}) => {
  const api = new Api(request);
  const cardId = await api.cardId(seed.cards.opsVan.name);
  await api.setLimit(cardId, 'fuel', 'month', '1234.56');
  const decision = await api.authorise({
    cardNumber: seed.cards.opsVan.cardNumber,
    amount: '10.00',
    category: 'fuel',
    merchant: 'Shell',
    timestamp: '2026-10-24T23:30:00Z',
  });
  expect(decision.body.outcome).toBe('approved');

  const detail = new CardDetailPage(page);
  await detail.goto(cardId);

  expect(await detail.limits()).toEqual([['Fuel', 'Month', '£1,234.56']]);
  expect((await detail.transactions())[0]?.time).toBe('25 Oct 2026 00:30');
  await expectNoSeriousViolations(page);
});
