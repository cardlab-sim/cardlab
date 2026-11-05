import { describe, expect, test } from 'vitest';
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
      ['limit_changed', '0', '1000000.00'],
      ['limit_set', null, '0'],
    ]);
  });

  test('removeLimit removes and audits only when a limit existed', async () => {
    const fake = new FakeApiClient({ cards: [card()] });
    await fake.setLimit('c1', 'fuel', 'day', '5.00');
    await fake.removeLimit('c1', 'fuel', 'day');
    await fake.removeLimit('c1', 'fuel', 'day');
    expect((await fake.getCard('c1')).limits).toEqual([]);
    const audit = await fake.listAudit('c1');
    expect(audit.map((a) => [a.action, a.oldValue, a.newValue])).toEqual([
      ['limit_removed', '5.00', null],
      ['limit_set', null, '5.00'],
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
      oldValue: 'active',
      newValue: 'frozen',
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
