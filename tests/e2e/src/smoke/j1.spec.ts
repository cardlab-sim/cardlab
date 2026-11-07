import { expect, test } from '@playwright/test';
import { CardDetailPage } from '../pages/CardDetailPage';
import { CardListPage } from '../pages/CardListPage';
import { seed } from '../seed';

// T-E-7 · PR-10 · e2e
// Given the deployed demo (no reset), When the admin opens Ops van, freezes, simulates £1.00 Fuel (declined card_frozen), unfreezes and simulates £1.00 Fuel (approved), Then each simulate result appears within 2 s of submit and the card ends active
// Catches: a deploy that serves but cannot decide, or a slow round trip (AC-36, G-1); leaving demo frozen
test('T-E-7 smoke on demo: J-1 on Ops van within 2 s, restoring state', async ({ page }) => {
  const list = new CardListPage(page);
  const detail = new CardDetailPage(page);
  await list.goto();
  await list.open(seed.cards.opsVan.name);

  // Whatever the starting state, end active (a previous failed run may have left the card frozen).
  try {
    if ((await detail.status()) === 'Frozen') {
      await detail.unfreeze();
    }
    await detail.freeze();
    const declined = await timed(() => detail.simulate('1.00', 'Fuel', 'Smoke test'));
    expect(declined.text).toContain('card_frozen');
    expect(declined.ms).toBeLessThan(2000);

    await detail.unfreeze();
    const approved = await timed(() => detail.simulate('1.00', 'Fuel', 'Smoke test'));
    expect(approved.text).toContain('Approved');
    expect(approved.ms).toBeLessThan(2000);
  } finally {
    if ((await detail.status()) === 'Frozen') {
      await detail.unfreeze();
    }
  }
  expect(await detail.status()).toBe('Active');
});

async function timed(action: () => Promise<string>): Promise<{ text: string; ms: number }> {
  const started = Date.now();
  const text = await action();
  return { text, ms: Date.now() - started };
}
