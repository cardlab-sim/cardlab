import { expect, test, type APIRequestContext } from '@playwright/test';
import { Api } from '../api';
import { PaymentRecordPage } from '../pages/PaymentRecordPage';
import { seed } from '../seed';

test.beforeEach(async ({ request }) => {
  await new Api(request).reset();
});

// T-E-6 · PR-08 · e2e
// Given a completed, a failed, a pending and a refused reference on Ops van, When the admin opens /cards/:id, Then the heading shows the balance owed, Activity lists payments as payments and the Payments section lists all four; opening each record shows the shown wording with version v1, acceptance, outcome and events
// Catches: the admin unable to audit what was shown and accepted (AC-20, AC-2)
test('T-E-6 admin sees balance, activity and each payment record', async ({ page, request }) => {
  // One real bank budget of 10 s for the pending payment.
  test.setTimeout(60_000);
  const api = new Api(request);
  const opsVan = await api.cardId(seed.cards.opsVan.name);
  const customer = `/api/cardholder/cards/${opsVan}`;

  await api.simulatePurchase(opsVan, '100.00');
  const completed = await quote(request, customer);
  await confirm(request, customer, completed);

  await api.simulatePurchase(opsVan, '50.00');
  await api.setBankOutcome('refuse_insufficient_funds');
  const failed = await quote(request, customer);
  await confirm(request, customer, failed);

  const refused = await quote(request, customer);
  await api.simulatePurchase(opsVan, '20.00');
  const refusal = await request.post(`${customer}/payments/${refused}/confirm`);
  expect(refusal.status(), 'confirm of a stale quote').toBe(409);

  await api.setBankOutcome('timeout_debit_applied');
  const pending = await quote(request, customer);
  const timedOut = await confirm(request, customer, pending);
  expect(timedOut, 'state after the bank timed out').toBe('pending');

  const record = new PaymentRecordPage(page);
  const wording = (amount: string): string[] => [
    `Amount: ${amount}`,
    'Fee: £0.00 (no fee)',
    `Total to pay: ${amount}`,
    'When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.',
    'From: Brightline Supplies Ltd current account, ending 7731',
    'To: Ops van card, ending 0001',
  ];

  await record.goto(opsVan, completed);
  expect(await record.shown()).toEqual({ lines: wording('£100.00'), version: 'v1' });
  const completedAcceptance = await record.section('Acceptance');
  expect(completedAcceptance).toContain(`${seed.cards.opsVan.cardholderName} (cardholder)`);
  expect(completedAcceptance).toContain(seed.linkedAccount.authorityBasis);
  const completedPayment = await record.section('Payment');
  expect(completedPayment).toContain('Completed');
  expect(completedPayment).toMatch(/Balance owed before\s+£100\.00/);
  expect(completedPayment).toMatch(/Balance owed after\s+£0\.00/);
  expect(await record.section('Events')).toContain('Completed');

  await record.goto(opsVan, failed);
  expect(await record.shown()).toEqual({ lines: wording('£50.00'), version: 'v1' });
  const failedPayment = await record.section('Payment');
  expect(failedPayment).toContain('Failed');
  expect(failedPayment).toContain('Insufficient funds');
  expect(await record.simulated()).toBe(true);

  await record.goto(opsVan, refused);
  expect(await record.shown()).toEqual({ lines: wording('£50.00'), version: 'v1' });
  const refusalSection = await record.section('Refusal');
  expect(refusalSection).toContain('balance_changed');
  expect(refusalSection).toMatch(/Balance owed at refusal\s+£70\.00/);

  await record.goto(opsVan, pending);
  expect(await record.shown()).toEqual({ lines: wording('£70.00'), version: 'v1' });
  const pendingPayment = await record.section('Payment');
  expect(pendingPayment).toContain('Pending');
  expect(pendingPayment).not.toContain('Balance owed before');
  expect(await record.origin()).toBe('Cardholder view');
  expect(await record.section('Events')).toContain('Pending');

  // Opening the admin card detail runs the automatic status check (AC-34): the debit the bank applied settles the
  // pending payment, so the balance owed drops to £0.00 and the record shows it completed.
  await page.goto(`/cards/${opsVan}`);
  await expect(page.getByText('Balance owed £0.00', { exact: true })).toBeVisible();
  const activity = page
    .getByRole('table', { name: 'Transaction history' })
    .getByRole('row')
    .filter({ has: page.getByRole('cell') });
  await expect(activity.filter({ hasText: 'Payment PAY-' })).toHaveCount(3);
  await expect(activity.filter({ hasText: 'Payment PAY-' }).getByRole('link')).toHaveCount(3);
  await expect(activity.filter({ hasText: 'Payment PAY-' }).getByText('Completed')).toHaveCount(2);
  const payments = page.getByRole('region', { name: 'Payments', exact: true });
  for (const reference of [completed, failed, refused, pending]) {
    await expect(payments.getByRole('link', { name: reference })).toBeVisible();
  }
  await record.goto(opsVan, pending);
  const settled = await record.section('Payment');
  expect(settled).toContain('Completed');
  expect(await record.shown()).toEqual({ lines: wording('£70.00'), version: 'v1' });
  expect(await record.section('Acceptance')).toContain(
    `${seed.cards.opsVan.cardholderName} (cardholder)`,
  );
  expect(settled).toMatch(/Balance owed before\s+£70\.00/);
  expect(settled).toMatch(/Balance owed after\s+£0\.00/);
  expect(await record.simulated()).toBe(true);
});

async function quote(request: APIRequestContext, customer: string): Promise<string> {
  const response = await request.post(`${customer}/quotes`);
  expect(response.status(), 'POST quote').toBe(201);
  return ((await response.json()) as { reference: string }).reference;
}

async function confirm(
  request: APIRequestContext,
  customer: string,
  reference: string,
): Promise<string> {
  const response = await request.post(`${customer}/payments/${reference}/confirm`);
  expect(response.status(), 'POST confirm').toBe(200);
  return ((await response.json()) as { state: string }).state;
}
