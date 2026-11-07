import { expect, test } from '@playwright/test';
import { expectNoSeriousViolations } from './a11y';
import { Api } from './api';
import { CardDetailPage } from './pages/CardDetailPage';
import { seed } from './seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-2 · PR-10 · e2e
// Given a reset system, When the admin sets Fuel/Day £50.00, changes it to £80.00, removes it, and tries -1.00 and 10.005, Then the table reflects each step, invalid amounts show an inline error and change nothing, and the audit list shows three events newest first with actor, time, action and old/new values
// Catches: audit order or content wrong in the UI; invalid amounts accepted (AC-6..AC-9, AC-23)
test('T-E-2 J-2 set, change and remove a limit; invalid amounts rejected', async ({
  page,
  request,
}) => {
  const detail = new CardDetailPage(page);
  await detail.goto(await new Api(request).cardId(seed.cards.opsVan.name));

  await detail.setLimit('Fuel', 'Day', '50.00');
  expect(await detail.limits()).toEqual([['Fuel', 'Day', '£50.00']]);

  await detail.setLimit('Fuel', 'Day', '80.00');
  expect(await detail.limits()).toEqual([['Fuel', 'Day', '£80.00']]);

  await detail.removeLimit('Fuel', 'Day');
  expect(await detail.limits()).toEqual([]);

  await detail.setLimit('Fuel', 'Day', '-1.00');
  expect(await detail.fieldError('Amount')).not.toBeNull();
  expect(await detail.limits()).toEqual([]);

  await detail.setLimit('Fuel', 'Day', '10.005');
  expect(await detail.fieldError('Amount')).not.toBeNull();
  expect(await detail.limits()).toEqual([]);
  await expectNoSeriousViolations(page);

  const audit = await detail.audit();
  expect(audit.map((a) => [a.action, a.actor, a.values])).toEqual([
    ['Limit removed', seed.adminName, 'Fuel day £80.00 → —'],
    ['Limit changed', seed.adminName, 'Fuel day £50.00 → Fuel day £80.00'],
    ['Limit set', seed.adminName, '— → Fuel day £50.00'],
  ]);
  for (const item of audit) {
    expect(item.time).toMatch(/^\d{1,2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}$/);
  }
});
