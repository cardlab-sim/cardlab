import { expect, type Locator, type Page } from '@playwright/test';

/** A recent card activity row as text. */
export interface ActivityRow {
  date: string;
  description: string;
  amount: string;
  type: string;
  state: string;
}

/** A current account activity row as text. */
export interface AccountActivityRow {
  date: string;
  reference: string;
  amount: string;
}

/**
 * 002 page object for `/cardholder/:id` (role/label locators only), against the accessible names fixed by PR-03:
 * button "Pay full balance", button "Check status" (one in "Balance" and one in "Pending payment": always scoped),
 * regions "Balance", "Pending payment", "Linked current account", "Recent card activity", "Current account recent
 * activity", "Fake bank (simulation control)" with selects "Next debit outcome" / "Next status check" and button "Set".
 */
export class CardholderPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** The "Balance" region. */
  get balanceRegion(): Locator {
    return this.page.getByRole('region', { name: 'Balance', exact: true });
  }

  /** The "Pending payment" region (present only while a payment is pending). */
  get pendingRegion(): Locator {
    return this.page.getByRole('region', { name: 'Pending payment' });
  }

  /** The "Linked current account" region. */
  get accountRegion(): Locator {
    return this.page.getByRole('region', { name: 'Linked current account' });
  }

  /** The fake bank control region. */
  get fakeBankRegion(): Locator {
    return this.page.getByRole('region', { name: 'Fake bank (simulation control)' });
  }

  async goto(cardId: string): Promise<void> {
    await this.page.goto(`/cardholder/${cardId}`);
    await this.waitUntilLoaded();
  }

  /** Waits for the view to be rendered (the Balance region is only there once loaded). */
  async waitUntilLoaded(timeoutMs?: number): Promise<void> {
    await expect(this.balanceRegion).toBeVisible({ timeout: timeoutMs });
  }

  /** The balance owed figure as shown, e.g. "£150.50". */
  async balance(): Promise<string> {
    return firstMoney(await this.balanceRegion.innerText());
  }

  /** The linked account's available balance as shown, e.g. "£25,000.00". */
  async accountBalance(): Promise<string> {
    return firstMoney(await this.accountRegion.innerText());
  }

  /** Presses "Pay full balance" and waits for the review screen. */
  async pay(): Promise<void> {
    await this.page.getByRole('button', { name: 'Pay full balance' }).click();
    await expect(this.page).toHaveURL(/\/cardholder\/[^/]+\/pay$/);
  }

  /** Recent card activity rows, newest first. */
  async readActivity(): Promise<ActivityRow[]> {
    const rows = await this.cells('Recent card activity');
    return rows.map(([date = '', description = '', amount = '', type = '', state = '']) => ({
      date,
      description,
      amount,
      type,
      state,
    }));
  }

  /** Current account recent activity rows, newest first. */
  async readAccountActivity(): Promise<AccountActivityRow[]> {
    const rows = await this.cells('Current account recent activity');
    return rows.map(([date = '', reference = '', amount = '']) => ({ date, reference, amount }));
  }

  /** Chooses options by their visible labels (e.g. "Refuse (insufficient funds)") and presses "Set". */
  async setFakeBank(outcomeLabel: string, statusCheckLabel?: string): Promise<void> {
    const region = this.fakeBankRegion;
    await region.getByLabel('Next debit outcome').selectOption({ label: outcomeLabel });
    if (statusCheckLabel !== undefined) {
      await region.getByLabel('Next status check').selectOption({ label: statusCheckLabel });
    }
    await region.getByRole('button', { name: 'Set' }).click();
    await expect(region.getByText(`next debit outcome ${outcomeLabel}`)).toBeVisible();
  }

  private async cells(caption: string): Promise<string[][]> {
    const rows = this.page
      .getByRole('table', { name: caption })
      .getByRole('row')
      .filter({ has: this.page.getByRole('cell') });
    const result: string[][] = [];
    for (const row of await rows.all()) {
      const cells = (await row.getByRole('cell').allInnerTexts()).map((t) => t.trim());
      if (cells.length > 1) {
        result.push(cells);
      }
    }
    return result;
  }
}

function firstMoney(text: string): string {
  const match = /£[\d,]+\.\d{2}/.exec(text);
  if (match === null) {
    throw new Error('No money amount found in the region');
  }
  return match[0];
}
