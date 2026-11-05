import { expect, type Page } from '@playwright/test';

/** Page object for `/` (role/label locators only). */
export class CardListPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** Navigates to `/` and waits for the cards table. */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await expect(this.page.getByRole('table', { name: 'Card list' })).toBeVisible();
  }

  /** Status badge text of a card row ("Active" / "Frozen"). */
  async statusOf(cardName: string): Promise<string> {
    const row = this.page
      .getByRole('table', { name: 'Card list' })
      .getByRole('row')
      .filter({ has: this.page.getByRole('link', { name: `Open ${cardName}` }) });
    return (await row.getByRole('cell').nth(3).innerText()).trim();
  }

  /** Follows "Open <card name>". */
  async open(cardName: string): Promise<void> {
    await this.page.getByRole('link', { name: `Open ${cardName}` }).click();
    await expect(this.page.getByRole('heading', { level: 1, name: cardName })).toBeVisible();
  }
}
