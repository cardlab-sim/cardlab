import { afterEach, describe, expect, test, vi } from 'vitest';
import { ApiError } from './apiClient';
import { FakeApiClient } from './fakeApiClient';
import type { CardDetail, Decision, SimulatePurchaseInput } from './types';

const card = (): CardDetail => ({
  id: 'c1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [],
  balanceOwed: '0.00',
});

const purchase: SimulatePurchaseInput = { amount: '12.50', category: 'fuel', merchant: 'Shell' };

const exceeded: Decision = {
  outcome: 'declined',
  reason: 'limit_exceeded',
  transactionId: null,
  limits: [],
  problems: [],
};

async function rejection(promise: Promise<unknown>): Promise<unknown> {
  return promise.then(
    () => undefined,
    (e: unknown) => e,
  );
}

// Harness tests: PR-07 relies on these behaviours of the shared fake (design 15.2).
describe('FakeApiClient', () => {
  test('rejects an invalid limit amount with a 400 and changes nothing', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    for (const amount of ['10.005', '-1.00', '1000000.01']) {
      const error = await rejection(fake.setLimit('c1', 'fuel', 'day', amount));
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(400);
      expect((error as ApiError).problem?.errors?.amount?.[0]).toBeTruthy();
    }
    expect((await fake.getCard('c1')).limits).toEqual([]);
    expect(await fake.listAudit('c1')).toEqual([]);
  });

  test('accepts the bounds and audits set then change', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    expect(await fake.setLimit('c1', 'fuel', 'day', '0')).toEqual({
      category: 'fuel',
      period: 'day',
      amount: '0',
    });
    await fake.setLimit('c1', 'fuel', 'day', '1000000.00');
    expect((await fake.getCard('c1')).limits).toEqual([
      { category: 'fuel', period: 'day', amount: '1000000.00' },
    ]);
    const audit = await fake.listAudit('c1');
    expect(audit.map((a) => [a.action, a.oldValue, a.newValue])).toEqual([
      ['limit_changed', 'Fuel/Day/0', 'Fuel/Day/1000000.00'],
      ['limit_set', null, 'Fuel/Day/0'],
    ]);
  });

  test('audit limit values use PascalCase category names', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    await fake.setLimit('c1', 'meals_and_entertainment', 'week', '10.00');
    const audit = await fake.listAudit('c1');
    expect(audit[0]?.newValue).toBe('MealsAndEntertainment/Week/10.00');
  });

  test('removeLimit removes and audits only when a limit existed', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    await fake.setLimit('c1', 'fuel', 'day', '5.00');
    await fake.removeLimit('c1', 'fuel', 'day');
    await fake.removeLimit('c1', 'fuel', 'day');
    expect((await fake.getCard('c1')).limits).toEqual([]);
    const audit = await fake.listAudit('c1');
    expect(audit.map((a) => [a.action, a.oldValue, a.newValue])).toEqual([
      ['limit_removed', 'Fuel/Day/5.00', null],
      ['limit_set', null, 'Fuel/Day/5.00'],
    ]);
  });

  test('freeze twice audits once and unfreezing an active card audits nothing', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    expect(await fake.unfreeze('c1')).toEqual({ status: 'active' });
    expect(await fake.listAudit('c1')).toEqual([]);

    await fake.freeze('c1');
    expect(await fake.freeze('c1')).toEqual({ status: 'frozen' });
    const audit = await fake.listAudit('c1');
    expect(audit).toHaveLength(1);
    expect(audit[0]).toMatchObject({
      action: 'freeze',
      actorName: 'Sam Ellis',
      oldValue: 'Active',
      newValue: 'Frozen',
    });
    expect((await fake.listCards())[0]?.status).toBe('frozen');
  });

  test('simulatePurchase stores the decided transaction first and clears nextDecision', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    await fake.simulatePurchase('c1', { ...purchase, merchant: 'First' });
    fake.nextDecision = exceeded;
    const decision = await fake.simulatePurchase('c1', purchase);
    expect(decision.reason).toBe('limit_exceeded');
    expect(fake.nextDecision).toBeNull();
    const transactions = await fake.listTransactions('c1');
    expect(transactions).toHaveLength(2);
    expect(transactions[0]).toMatchObject({
      outcome: 'declined',
      reason: 'limit_exceeded',
      amount: '12.50',
      merchant: 'Shell',
    });
    expect(transactions[1]).toMatchObject({ outcome: 'approved', merchant: 'First' });
  });

  test('an invalid_request decision stores no transaction', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    fake.nextDecision = { ...exceeded, reason: 'invalid_request' };
    await fake.simulatePurchase('c1', purchase);
    expect(await fake.listTransactions('c1')).toEqual([]);
  });

  test('simulatePurchase on an unknown card resolves with card_not_found', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    fake.nextDecision = exceeded;
    await expect(fake.simulatePurchase('nope', purchase)).resolves.toMatchObject({
      reason: 'card_not_found',
      transactionId: null,
    });
    expect(fake.nextDecision).toBe(exceeded);
  });

  test('unknown ids reject with a 404', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    for (const call of [
      () => fake.getCard('nope'),
      () => fake.freeze('nope'),
      () => fake.setLimit('nope', 'fuel', 'day', '1.00'),
      () => fake.listTransactions('nope'),
      () => fake.listAudit('nope'),
    ]) {
      const error = await rejection(call());
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).status).toBe(404);
    }
  });

  test('hold keeps calls pending until released', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    const release = fake.hold();
    let settled = false;
    const pending = fake.listCards().then((cards) => {
      settled = true;
      return cards;
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(settled).toBe(false);
    release();
    expect(await pending).toHaveLength(1);
    expect(fake.calls).toEqual(['listCards']);
  });

  test('results are copies of the fake state', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    const first = await fake.getCard('c1');
    first.name = 'Changed';
    first.limits.push({ category: 'fuel', period: 'day', amount: '1.00' });
    const second = await fake.getCard('c1');
    expect(second.name).toBe('Ops van');
    expect(second.limits).toEqual([]);
  });
});

const opsVan = (): CardDetail => ({ ...card(), id: 'ops-1', balanceOwed: '0.00' });

const ACCOUNT = 'Brightline Supplies Ltd current account, ending 7731';

const seeded = (purchases = ['150.50']) =>
  new FakeApiClient({ cards: [opsVan()], purchases: { 'ops-1': purchases } });

const W5 = (balance: string, reference: string) =>
  `Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows ${balance} until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference ${reference}.`;

afterEach(() => {
  vi.useRealTimers();
});

describe('FakeApiClient (002)', () => {
  // T-W-10 · PR-03 · web unit
  // Given a seed with Ops van, purchases £150.50 and the seeded account at £25,000.00, When createQuote → confirmPayment, Then a W-1 quote, a completed receipt with W-2, balance £0.00, account £24,849.50 with one entry; withBank('refuse_insufficient_funds') gives a failed W-3 receipt; withBank('timeout_not_received') a pending W-5 receipt and checkPayment then a failed W-4 receipt; a changed balance rejects 409 balance_changed with W-6; hold() keeps confirm pending; calls are recorded
  // Catches: page tests passing against a fake that behaves unlike the API (design §15.2)
  test('T-W-10 completes a payment: W-1 quote, W-2 receipt, balance and account updated', async () => {
    const fake = seeded();
    expect((await fake.getCard('ops-1')).balanceOwed).toBe('150.50');

    const quote = await fake.createQuote('ops-1');
    expect(quote.reference).toMatch(/^PAY-[0-9A-HJKMNP-TV-Z]{8}$/);
    expect([quote.amount, quote.fee, quote.total]).toEqual(['150.50', '0.00', '150.50']);
    expect(quote.wording.heading).toBe('Pay your full balance');
    expect(quote.wording.lines).toEqual([
      'Amount: £150.50',
      'Fee: £0.00 (no fee)',
      'Total to pay: £150.50',
      'When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.',
      `From: ${ACCOUNT}`,
      'To: Ops van card, ending 0001',
    ]);
    expect(quote.wording.button).toBe('Confirm payment of £150.50');

    const receipt = await fake.confirmPayment('ops-1', quote.reference);
    expect(receipt).toMatchObject({
      reference: quote.reference,
      state: 'completed',
      amount: '150.50',
      fee: '0.00',
      from: ACCOUNT,
      to: 'Ops van card, ending 0001',
      failureReason: null,
      balanceOwed: '0.00',
      repeated: false,
      origin: 'cardholder_view',
      message: `Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference ${quote.reference}.`,
    });

    expect((await fake.getCard('ops-1')).balanceOwed).toBe('0.00');
    const view = await fake.getCardholderView('ops-1');
    expect(view.account.availableBalance).toBe('24849.50');
    expect(view.accountActivity).toHaveLength(1);
    expect(view.accountActivity[0]).toMatchObject({ reference: quote.reference, amount: '150.50' });
    expect(view.availability).toBe('nothing_to_pay');
    expect(view.availabilityMessage).toBe('Nothing to pay. Your card balance is £0.00.');
    expect(view.cardActivity.map((a) => [a.kind, a.amount, a.state])).toEqual([
      ['payment', '150.50', 'completed'],
      ['purchase', '150.50', null],
    ]);
    expect(await fake.getReceipt('ops-1', quote.reference)).toEqual(receipt);
  });

  test('T-W-10 refusal by the bank fails the payment and moves nothing', async () => {
    const fake = seeded().withBank('refuse_insufficient_funds');
    const quote = await fake.createQuote('ops-1');
    const receipt = await fake.confirmPayment('ops-1', quote.reference);

    expect(receipt.state).toBe('failed');
    expect(receipt.failureReason).toBe('insufficient_funds');
    expect(receipt.message).toBe(
      `Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still £150.50. Add money to the account, then try again. Reference ${quote.reference}.`,
    );
    const view = await fake.getCardholderView('ops-1');
    expect(view.balanceOwed).toBe('150.50');
    expect(view.account.availableBalance).toBe('25000.00');
    expect(view.accountActivity).toEqual([]);
    // The outcome is one-shot: the next payment completes.
    const again = await fake.createQuote('ops-1');
    expect((await fake.confirmPayment('ops-1', again.reference)).state).toBe('completed');
  });

  test('T-W-10 account unavailable fails with its reason', async () => {
    const fake = seeded().withBank('refuse_account_unavailable');
    const quote = await fake.createQuote('ops-1');
    const receipt = await fake.confirmPayment('ops-1', quote.reference);
    expect([receipt.state, receipt.failureReason]).toEqual(['failed', 'account_unavailable']);
    expect(receipt.message).toBe(
      `Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference ${quote.reference}.`,
    );
  });

  test('T-W-10 refuses a payment above the account balance as insufficient funds', async () => {
    const fake = new FakeApiClient({
      cards: [opsVan()],
      purchases: { 'ops-1': ['150.50'] },
      account: { availableBalance: '100.00' },
    });
    const quote = await fake.createQuote('ops-1');
    const receipt = await fake.confirmPayment('ops-1', quote.reference);
    expect([receipt.state, receipt.failureReason]).toEqual(['failed', 'insufficient_funds']);
  });

  test('T-W-10 a timeout is pending (W-5) and blocks a new quote with W-9; unanswered checks stay pending (W-10)', async () => {
    const fake = seeded().withBank('timeout_not_received').withStatusCheck('does_not_answer');
    const quote = await fake.createQuote('ops-1');
    const receipt = await fake.confirmPayment('ops-1', quote.reference);
    expect(receipt.state).toBe('pending');
    expect(receipt.message).toBe(W5('£150.50', quote.reference));

    const view = await fake.getCardholderView('ops-1');
    expect(view.pendingPayment?.reference).toBe(quote.reference);
    expect(view.availability).toBe('payment_pending');
    expect(view.availabilityMessage).toBe(
      `A payment of £150.50 (reference ${quote.reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.`,
    );

    const blocked = await rejection(fake.createQuote('ops-1'));
    expect(blocked).toBeInstanceOf(ApiError);
    expect((blocked as ApiError).status).toBe(409);
    expect((blocked as ApiError).problem).toMatchObject({
      code: 'payment_pending',
      message: view.availabilityMessage,
      amount: '150.50',
      pendingReference: quote.reference,
      balanceOwed: '150.50',
    });

    const unanswered = await fake.checkPayment('ops-1', quote.reference);
    expect(unanswered.state).toBe('pending');
    expect(unanswered.message).toBe(
      `Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference ${quote.reference}.`,
    );
    expect((await fake.getCardholderView('ops-1')).account.availableBalance).toBe('25000.00');
  });

  test('T-W-10 a check that is answered fails a not-received payment with W-4 wording', async () => {
    const fake = seeded().withBank('timeout_not_received');
    const quote = await fake.createQuote('ops-1');
    await fake.confirmPayment('ops-1', quote.reference);

    const checked = await fake.checkPayment('ops-1', quote.reference);
    expect([checked.state, checked.failureReason]).toEqual(['failed', 'not_received']);
    expect(checked.message).toBe(
      `Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference ${quote.reference}.`,
    );
    expect((await fake.getCardholderView('ops-1')).balanceOwed).toBe('150.50');
  });

  test('T-W-10 a debit the bank applied during a timeout completes at the bank time on check', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-07T10:00:00Z'));
    const fake = seeded().withBank('timeout_debit_applied');
    const quote = await fake.createQuote('ops-1');
    const pending = await fake.confirmPayment('ops-1', quote.reference);
    expect(pending.state).toBe('pending');
    // The view is a pure read: still pending.
    expect((await fake.getCardholderView('ops-1')).pendingPayment?.state).toBe('pending');

    vi.setSystemTime(new Date('2026-11-07T10:05:00Z'));
    const checked = await fake.checkPayment('ops-1', quote.reference);
    expect(checked.state).toBe('completed');
    expect(checked.time).toBe('2026-11-07T10:00:00.000Z');
    expect(checked.balanceOwed).toBe('0.00');
    expect((await fake.getCardholderView('ops-1')).account.availableBalance).toBe('24849.50');
  });

  test('T-W-10 a balance change after the quote rejects the confirm with 409 balance_changed (W-6)', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    fake.addPurchase('ops-1', '20.00');

    const error = await rejection(fake.confirmPayment('ops-1', quote.reference));
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).problem).toMatchObject({
      code: 'balance_changed',
      balanceOwed: '170.50',
      message:
        'Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.',
    });
    const view = await fake.getCardholderView('ops-1');
    expect(view.balanceOwed).toBe('170.50');
    expect(view.account.availableBalance).toBe('25000.00');
    expect(view.pendingPayment).toBeNull();
    expect(((await rejection(fake.getReceipt('ops-1', quote.reference))) as ApiError).status).toBe(
      404,
    );
  });

  test('T-W-10 refuses quotes for nothing to pay (W-7) and above the limit (W-8)', async () => {
    const none = await rejection(seeded([]).createQuote('ops-1'));
    expect((none as ApiError).problem).toMatchObject({
      code: 'nothing_to_pay',
      message: 'Nothing to pay. Your card balance is £0.00.',
    });

    const big = seeded(['1000000.01']);
    const view = await big.getCardholderView('ops-1');
    expect(view.availability).toBe('above_limit');
    expect(view.availabilityMessage).toBe(
      'This balance cannot be paid here. Payments above £1,000,000.00 are not available online. No money has moved. Your card balance is £1,000,000.01. Contact your account administrator.',
    );
    const refused = await rejection(big.createQuote('ops-1'));
    expect((refused as ApiError).problem?.code).toBe('above_limit');
  });

  test('T-W-10 steering is read and changed through getFakeBank and setFakeBank', async () => {
    const fake = seeded();
    expect(await fake.getFakeBank()).toEqual({
      nextDebitOutcome: 'complete',
      nextStatusCheck: 'answers',
      account: { lastFour: '7731', availableBalance: '25000.00' },
      entries: [],
    });
    const updated = await fake.setFakeBank({ nextStatusCheck: 'does_not_answer' });
    expect([updated.nextDebitOutcome, updated.nextStatusCheck]).toEqual([
      'complete',
      'does_not_answer',
    ]);
  });

  test('T-W-10 lists payment records and returns one with what was shown', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    expect((await fake.listPaymentRecords('ops-1')).map((p) => [p.reference, p.status])).toEqual([
      [quote.reference, 'open'],
    ]);
    await fake.confirmPayment('ops-1', quote.reference);

    const list = await fake.listPaymentRecords('ops-1');
    expect(list[0]).toMatchObject({ status: 'confirmed', state: 'completed', amount: '150.50' });
    const record = await fake.getPaymentRecord('ops-1', quote.reference);
    expect(record.quote.wording.button).toBe('Confirm payment of £150.50');
    expect(record.payment?.state).toBe('completed');
    expect(record.acceptance.actor).toEqual({ kind: 'cardholder', name: 'Dana Fox' });
    expect((await fake.listActivity('ops-1')).map((a) => a.kind)).toEqual(['payment', 'purchase']);
    expect(((await rejection(fake.getPaymentRecord('ops-1', 'PAY-nope'))) as ApiError).status).toBe(
      404,
    );
  });

  test('T-W-10 references are sequential PAY- plus eight digits and records are readable by them', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    expect(quote.reference).toBe('PAY-00000001');
    expect((await fake.getPaymentRecord('ops-1', 'PAY-00000001')).quote.reference).toBe(
      'PAY-00000001',
    );
  });

  test('T-W-10 records carry events and are simulated only when steered', async () => {
    const plain = seeded();
    const q1 = await plain.createQuote('ops-1');
    await plain.confirmPayment('ops-1', q1.reference);
    const r1 = await plain.getPaymentRecord('ops-1', q1.reference);
    expect(r1.payment?.simulated).toBe(false);
    expect(r1.events.map((e) => e.kind)).toEqual([
      'confirm_created',
      'debit_requested',
      'bank_completed',
    ]);

    const steered = seeded().withBank('refuse_insufficient_funds');
    const q2 = await steered.createQuote('ops-1');
    await steered.confirmPayment('ops-1', q2.reference);
    const r2 = await steered.getPaymentRecord('ops-1', q2.reference);
    expect(r2.payment?.simulated).toBe(true);
    expect(r2.events.map((e) => [e.kind, e.simulated])).toEqual([
      ['confirm_created', false],
      ['debit_requested', false],
      ['bank_refused', true],
    ]);
    expect(r2.events[2]).toMatchObject({
      from: 'pending',
      to: 'failed',
      detail: 'insufficient_funds',
    });

    const timeout = seeded().withBank('timeout_not_received');
    const q3 = await timeout.createQuote('ops-1');
    await timeout.confirmPayment('ops-1', q3.reference);
    const r3 = await timeout.getPaymentRecord('ops-1', q3.reference);
    expect([r3.payment?.balanceBefore, r3.payment?.balanceAfter]).toEqual([null, null]);
  });

  test('T-W-10 a refused quote has a record with its refusal', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    fake.addPurchase('ops-1', '20.00');
    await rejection(fake.confirmPayment('ops-1', quote.reference));
    const record = await fake.getPaymentRecord('ops-1', quote.reference);
    expect([record.status, record.refusalReason, record.balanceAtRefusal, record.payment]).toEqual([
      'refused',
      'balance_changed',
      '170.50',
      null,
    ]);
    expect(record.events.map((e) => [e.kind, e.detail])).toEqual([
      ['confirm_refused', 'balance_changed'],
    ]);
  });

  test('T-W-10 confirming an already refused reference is refused again with quote_refused (AC-30)', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    fake.addPurchase('ops-1', '20.00');
    const first = (await rejection(fake.confirmPayment('ops-1', quote.reference))) as ApiError;
    const second = (await rejection(fake.confirmPayment('ops-1', quote.reference))) as ApiError;

    expect(second.status).toBe(409);
    expect(second.problem?.code).toBe('quote_refused');
    expect(second.problem?.message).toBe(first.problem?.message);
    expect(second.problem?.balanceOwed).toBe('170.50');
    expect(((await rejection(fake.getReceipt('ops-1', quote.reference))) as ApiError).status).toBe(
      404,
    );
  });

  test('T-W-10 the status-check steering applies once, then reverts to answering (DEC-7)', async () => {
    const fake = seeded().withBank('timeout_not_received').withStatusCheck('does_not_answer');
    const quote = await fake.createQuote('ops-1');
    await fake.confirmPayment('ops-1', quote.reference);

    const first = await fake.checkPayment('ops-1', quote.reference);
    expect(first.state).toBe('pending');
    expect(first.message).toMatch(/^Still pending\./);
    expect((await fake.getFakeBank()).nextStatusCheck).toBe('answers');

    const second = await fake.checkPayment('ops-1', quote.reference);
    expect([second.state, second.failureReason]).toEqual(['failed', 'not_received']);
  });

  test('T-W-10 hold keeps confirm pending and calls are recorded', async () => {
    const fake = seeded();
    const quote = await fake.createQuote('ops-1');
    const release = fake.hold();
    let settled = false;
    const confirming = fake.confirmPayment('ops-1', quote.reference).then((r) => {
      settled = true;
      return r;
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(settled).toBe(false);
    release();
    expect((await confirming).state).toBe('completed');
    expect(fake.calls).toEqual(['createQuote', 'confirmPayment']);
  });

  test('T-W-10 unknown card ids reject with 404', async () => {
    const fake = seeded();
    for (const call of [
      fake.getCardholderView('nope'),
      fake.createQuote('nope'),
      fake.confirmPayment('nope', 'PAY-1'),
      fake.checkPayment('nope', 'PAY-1'),
      fake.listActivity('nope'),
    ]) {
      expect(((await rejection(call)) as ApiError).status).toBe(404);
    }
  });

  // T-W-10 · PR-03f · web unit (BUG-007)
  // Given purchases of £150.50, When a £12.50 purchase is simulated, Then balance owed is £163.00 and activity lists it first
  // Catches: the fake drifting from R-1 (simulated purchases not feeding balance or activity)
  test('T-W-10 a simulated purchase appears in activity and raises the balance owed', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-07-01T12:00:00Z') });
    const fake = seeded();

    await fake.simulatePurchase('ops-1', purchase);

    expect((await fake.getCard('ops-1')).balanceOwed).toBe('163.00');
    expect((await fake.getCardholderView('ops-1')).balanceOwed).toBe('163.00');
    const activity = await fake.listActivity('ops-1');
    expect(activity.map((a) => [a.description, a.amount, a.category])).toEqual([
      ['Shell', '12.50', 'fuel'],
      ['Card purchase', '150.50', 'other'],
    ]);
    expect(activity[0]).toMatchObject({
      kind: 'purchase',
      outcome: 'approved',
      reason: 'approved',
      at: '2026-07-01T12:00:00.000Z',
    });
  });

  // T-W-10 · PR-03f · web unit (BUG-007)
  // Given a declined simulated purchase, When listing, Then it shows in activity with its outcome and reason but the balance owed is unchanged
  // Catches: declined purchases billed to the card
  test('T-W-10 a declined simulated purchase is listed but does not raise the balance', async () => {
    const fake = seeded();
    fake.nextDecision = exceeded;

    await fake.simulatePurchase('ops-1', purchase);

    expect((await fake.getCard('ops-1')).balanceOwed).toBe('150.50');
    expect((await fake.listActivity('ops-1'))[0]).toMatchObject({
      description: 'Shell',
      amount: '12.50',
      outcome: 'declined',
      reason: 'limit_exceeded',
    });
  });

  // T-W-10 · PR-03f · web unit (BUG-007)
  // Given purchases decided and a payment confirmed at different times, When listing, Then activity is newest first by decision time (purchases, ignoring a client timestamp) and confirm time (payments, unmoved by a later status check), like design §5
  // Catches: rows in insertion order; a client-supplied timestamp or the settlement time used as the activity time
  test('T-W-10 activity is ordered newest first by decision and confirm time', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-07-01T12:00:00Z') });
    const fake = seeded();
    vi.setSystemTime(new Date('2026-07-01T12:03:00Z'));
    await fake.simulatePurchase('ops-1', { ...purchase, merchant: 'Early' });
    const quote = await fake.createQuote('ops-1');
    vi.setSystemTime(new Date('2026-07-01T12:05:00Z'));
    fake.withBank('timeout_not_received');
    await fake.confirmPayment('ops-1', quote.reference);
    vi.setSystemTime(new Date('2026-07-01T12:10:00Z'));
    await fake.simulatePurchase('ops-1', {
      ...purchase,
      merchant: 'Late',
      timestamp: '2026-06-01T08:00:00Z',
    });
    vi.setSystemTime(new Date('2026-07-01T12:20:00Z'));
    await fake.checkPayment('ops-1', quote.reference);

    const activity = await fake.listActivity('ops-1');

    expect(activity.map((a) => [a.description, a.at])).toEqual([
      ['Late', '2026-07-01T12:10:00.000Z'],
      [`Payment ${quote.reference}`, '2026-07-01T12:05:00.000Z'],
      ['Early', '2026-07-01T12:03:00.000Z'],
      ['Card purchase', '2026-07-01T12:00:00.000Z'],
    ]);
    expect((await fake.getCard('ops-1')).balanceOwed).toBe('175.50');
  });

  // T-W-10 · PR-03f · web unit (BUG-007)
  // Given the next quote origin is deploy_smoke, When quoted and confirmed, Then origin shows on the record, receipt, activity and payments list; the following quote is back to cardholder_view
  // Catches: a hard-coded origin
  test('T-W-10 a quote origin of deploy_smoke round-trips and is one-shot', async () => {
    const fake = seeded();
    fake.withQuoteOrigin('deploy_smoke');
    const quote = await fake.createQuote('ops-1');
    expect(quote.origin).toBe('deploy_smoke');
    const receipt = await fake.confirmPayment('ops-1', quote.reference);

    expect(receipt.origin).toBe('deploy_smoke');
    expect((await fake.getPaymentRecord('ops-1', quote.reference)).acceptance.origin).toBe(
      'deploy_smoke',
    );
    expect((await fake.listPaymentRecords('ops-1'))[0]?.origin).toBe('deploy_smoke');
    expect((await fake.listActivity('ops-1'))[0]?.origin).toBe('deploy_smoke');

    await fake.simulatePurchase('ops-1', purchase);
    expect((await fake.createQuote('ops-1')).origin).toBe('cardholder_view');
  });
});
