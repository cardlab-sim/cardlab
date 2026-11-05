import { expect, test } from '@playwright/test';
import { expectNoSeriousViolations } from './a11y';
import { Api } from './api';
import { CardDetailPage } from './pages/CardDetailPage';
import { CardListPage } from './pages/CardListPage';
import { seed } from './seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-1 · PR-10 · e2e
// Given a reset system, When the admin opens Ops van from the list, freezes it, simulates £1.00 Fuel, unfreezes and simulates again, Then the badge reads Frozen, the first purchase shows declined card_frozen, the second approved, and the audit list shows unfreeze then freeze by Sam Ellis
// Catches: the deployed UI not wiring freeze to the decision path (AC-1, AC-3, AC-4, AC-5, G-1)
test('T-E-1 J-1 freeze and unfreeze a card', async ({ page }) => {
  const list = new CardListPage(page);
  const detail = new CardDetailPage(page);
  await list.goto();
  expect(await list.statusOf(seed.cards.opsVan.name)).toBe('Active');
  await list.open(seed.cards.opsVan.name);

  await detail.freeze();
  expect(await detail.status()).toBe('Frozen');
  const declined = await detail.simulate('1.00', 'Fuel', 'Shell');
  expect(declined).toContain('Declined');
  expect(declined).toContain('card_frozen');
  expect((await detail.transactions())[0]).toMatchObject({
    amount: '£1.00',
    outcome: 'Declined',
    reason: 'Card frozen',
  });

  await detail.unfreeze();
  expect(await detail.status()).toBe('Active');
  const approved = await detail.simulate('1.00', 'Fuel', 'Shell');
  expect(approved).toContain('Approved');
  expect((await detail.transactions())[0]).toMatchObject({ outcome: 'Approved' });

  const audit = await detail.audit();
  expect(audit.map((a) => [a.action, a.actor, a.values])).toEqual([
    ['Card unfrozen', seed.adminName, 'Frozen → Active'],
    ['Card frozen', seed.adminName, 'Active → Frozen'],
  ]);

  await list.goto();
  expect(await list.statusOf(seed.cards.opsVan.name)).toBe('Active');
  await expectNoSeriousViolations(page);
});
