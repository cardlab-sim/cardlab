import { expect, type Locator, type Page } from '@playwright/test';

/** A transactions table row as text. */
export interface TransactionRow {
  time: string;
  merchant: string;
  category: string;
  amount: string;
  outcome: string;
  reason: string;
}

/** An audit list item as text. */
export interface AuditItem {
  action: string;
  actor: string;
  time: string;
  values: string;
}

/** Page object for `/cards/:id` (role/label locators only; Amount/Category are scoped by region). */
export class CardDetailPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  private get limitsRegion(): Locator {
    return this.page.getByRole('region', { name: 'Spend limits' });
  }

  private get simulateRegion(): Locator {
    return this.page.getByRole('region', { name: 'Simulate purchase' });
  }

  private get auditRegion(): Locator {
    return this.page.getByRole('region', { name: 'Audit' });
  }

  async goto(cardId: string): Promise<void> {
    await this.page.goto(`/cards/${cardId}`);
    await expect(this.page.getByRole('heading', { level: 1 })).toBeVisible();
  }

  /** Status badge text in the card header ("Active" / "Frozen"). */
  async status(): Promise<string> {
    const header = this.page
      .locator('header')
      .filter({ has: this.page.getByRole('heading', { level: 1 }) });
    return (
      await header
        .getByText(/^(Active|Frozen)$/)
        .first()
        .innerText()
    ).trim();
  }

  async freeze(): Promise<void> {
    await this.page.getByRole('button', { name: 'Freeze', exact: true }).click();
    await expect(this.page.getByRole('button', { name: 'Unfreeze', exact: true })).toBeVisible();
    await expect(this.page.getByRole('status').filter({ hasText: 'Card frozen' })).toBeVisible();
  }

  async unfreeze(): Promise<void> {
    await this.page.getByRole('button', { name: 'Unfreeze', exact: true }).click();
    await expect(this.page.getByRole('button', { name: 'Freeze', exact: true })).toBeVisible();
    await expect(this.page.getByRole('status').filter({ hasText: 'Card unfrozen' })).toBeVisible();
  }

  /** Fills category/period/amount (labels from the UI, e.g. "Fuel", "Day") and saves; waits for the table to update or an inline error. */
  async setLimit(categoryLabel: string, periodLabel: string, amount: string): Promise<void> {
    const region = this.limitsRegion;
    await region.getByLabel('Category').selectOption({ label: categoryLabel });
    await region.getByLabel('Period').selectOption({ label: periodLabel });
    await region.getByLabel('Amount').fill(amount);
    await region.getByRole('button', { name: 'Save' }).click();
    await expect(
      region.getByRole('status').filter({ hasText: 'Limit saved' }).or(region.getByRole('alert')),
    ).toBeVisible();
  }

  async removeLimit(categoryLabel: string, periodLabel: string): Promise<void> {
    const region = this.limitsRegion;
    await region
      .getByRole('button', {
        name: `Remove ${categoryLabel} ${periodLabel.toLowerCase()} limit`,
      })
      .click();
    await expect(region.getByRole('status').filter({ hasText: 'Limit removed' })).toBeVisible();
  }

  /** Limits table rows as [category, period, amount] text. */
  async limits(): Promise<string[][]> {
    const rows = this.limitsRegion.getByRole('table', { name: 'Limits' }).locator('tbody tr');
    const result: string[][] = [];
    for (const row of await rows.all()) {
      const cells = await row.getByRole('cell').allInnerTexts();
      if (cells.length >= 4) {
        result.push(cells.slice(0, 3).map((c) => c.trim()));
      }
    }
    return result;
  }

  /** Submits the simulate form and returns the result notice text once shown. */
  async simulate(amount: string, categoryLabel: string, merchant: string): Promise<string> {
    const region = this.simulateRegion;
    await region.getByLabel('Amount').fill(amount);
    await region.getByLabel('Category').selectOption({ label: categoryLabel });
    await region.getByLabel('Merchant').fill(merchant);
    await region.getByRole('button', { name: 'Submit' }).click();
    const result = region.getByRole('status').filter({ hasText: 'Result' });
    await expect(result).toBeVisible();
    await expect(region.getByRole('button', { name: 'Submit' })).toBeEnabled();
    return (await result.innerText()).trim();
  }

  async transactions(): Promise<TransactionRow[]> {
    const rows = this.page.getByRole('table', { name: 'Transaction history' }).locator('tbody tr');
    const result: TransactionRow[] = [];
    for (const row of await rows.all()) {
      const c = (await row.getByRole('cell').allInnerTexts()).map((t) => t.trim());
      if (c.length === 6) {
        result.push({
          time: c[0] ?? '',
          merchant: c[1] ?? '',
          category: c[2] ?? '',
          amount: c[3] ?? '',
          outcome: c[4] ?? '',
          reason: c[5] ?? '',
        });
      }
    }
    return result;
  }

  async audit(): Promise<AuditItem[]> {
    const items = this.auditRegion.getByRole('listitem');
    const result: AuditItem[] = [];
    for (const item of await items.all()) {
      const spans = (await item.locator('strong, span').allInnerTexts()).map((t) => t.trim());
      const [action = '', meta = '', values = ''] = spans;
      const [actor = '', time = ''] = meta.split(' · ');
      result.push({ action, actor, time, values });
    }
    return result;
  }

  /** Inline error text under a labelled field (limits form first, then simulate form), or null. */
  async fieldError(label: string): Promise<string | null> {
    for (const region of [this.limitsRegion, this.simulateRegion]) {
      const input = region.getByLabel(label);
      if ((await input.count()) === 0) {
        continue;
      }
      const describedBy = await input.getAttribute('aria-describedby');
      if (describedBy) {
        return (await this.page.locator(`#${describedBy}`).innerText()).trim();
      }
    }
    return null;
  }
}
