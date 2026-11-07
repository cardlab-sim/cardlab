import { expect, type Page } from '@playwright/test';

/** 002 page object for `/cardholder/:id/pay` (the one review screen). */
export class ReviewPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** The W-1 heading and lines in DOM order, each as one line of text ("Amount: £150.50"). */
  async lines(): Promise<string[]> {
    const main = this.page.getByRole('main');
    await expect(
      main.getByRole('heading', { level: 1, name: 'Pay your full balance' }),
    ).toBeVisible();
    const heading = (await main.getByRole('heading', { level: 1 }).innerText()).trim();
    const terms = await main.getByRole('term').allInnerTexts();
    const definitions = await main.getByRole('definition').allInnerTexts();
    const lines = terms.map((term, index) =>
      `${term} ${definitions[index] ?? ''}`.replace(/\s+/g, ' ').trim(),
    );
    return [heading, ...lines];
  }

  /** Clicks the confirm button (named "Confirm payment of £…"). */
  async confirm(): Promise<void> {
    await this.page.getByRole('button', { name: /^Confirm payment of £/ }).click();
  }
}
