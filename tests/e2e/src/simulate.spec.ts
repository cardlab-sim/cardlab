import { expect, test } from '@playwright/test';
import { expectNoSeriousViolations } from './a11y';
import { Api } from './api';
import { CardDetailPage } from './pages/CardDetailPage';
import { seed } from './seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-3 · PR-10 · e2e
// Given Field sales with Fuel/Day £50.00, When the admin submits £50.00 then £0.01 Fuel with a merchant, Then each shows a pending state then a notice with the outcome and reason code (approved, then limit_exceeded with remaining £0.00) and the latest transaction is the first table row
// Catches: result not shown or history not refreshed (AC-22, §4.1 submit feedback)
test('T-E-3 J-3 simulate a purchase shows the decision and refreshes the history', async ({
  page,
  request,
}) => {
  const api = new Api(request);
  const cardId = await api.cardId(seed.cards.fieldSales.name);
  await api.setLimit(cardId, 'fuel', 'day', '50.00');
  const detail = new CardDetailPage(page);
  await detail.goto(cardId);

  // Hold the response briefly so the pending state is observable deterministically.
  await page.route('**/simulate-purchase', async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 400));
    await route.continue();
  });
  const simulate = page.getByRole('region', { name: 'Simulate purchase' });

  const approvedPromise = detail.simulate('50.00', 'Fuel', 'BP Fuel Stop');
  await expect(simulate.getByRole('button', { name: 'Simulating…' })).toBeVisible();
  const approved = await approvedPromise;
  expect(approved).toContain('Approved');
  expect(approved).toContain('approved');
  expect((await detail.transactions())[0]).toMatchObject({
    merchant: 'BP Fuel Stop',
    amount: '£50.00',
    outcome: 'Approved',
  });

  const exceededPromise = detail.simulate('0.01', 'Fuel', 'BP Fuel Stop');
  await expect(simulate.getByRole('button', { name: 'Simulating…' })).toBeVisible();
  const exceeded = await exceededPromise;
  expect(exceeded).toContain('Declined');
  expect(exceeded).toContain('limit_exceeded');
  expect(exceeded).toContain('£0.00 remaining');
  const rows = await detail.transactions();
  expect(rows).toHaveLength(2);
  expect(rows[0]).toMatchObject({ amount: '£0.01', outcome: 'Declined', reason: 'Limit exceeded' });
  await expectNoSeriousViolations(page);
});
