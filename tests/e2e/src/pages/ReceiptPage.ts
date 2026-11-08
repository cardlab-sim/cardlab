import { expect, type Page } from '@playwright/test';

/** 002 page object for `/cardholder/:id/payments/:reference`. */
export class ReceiptPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** The payment reference from the receipt URL. */
  reference(): string {
    const reference = /\/payments\/([^/?#]+)/.exec(this.page.url())?.[1];
    if (reference === undefined) {
      throw new Error('The page is not a receipt');
    }
    return decodeURIComponent(reference);
  }

  /** The outcome badge text (Completed / Failed / Pending). */
  async state(): Promise<string> {
    const outcome = (await this.fields())['Outcome'] ?? '';
    const match = /^(Completed|Failed|Pending)/.exec(outcome);
    if (match === null) {
      throw new Error(`Unexpected outcome text "${outcome}"`);
    }
    return match[1] ?? '';
  }

  /** The outcome message (W-2..W-5, W-10) as shown. */
  async message(): Promise<string> {
    const main = this.page.getByRole('main');
    const notice = main.getByRole('status').or(main.getByRole('alert')).first();
    await expect(notice).toBeVisible();
    return (await notice.innerText()).replace(/\s+/g, ' ').trim();
  }

  /** The `<dl>` fields by term (Reference, Amount, Fee, Time, From, To, Outcome). */
  async fields(): Promise<Record<string, string>> {
    const main = this.page.getByRole('main');
    await expect(main.getByRole('term').first()).toBeVisible();
    const terms = await main.getByRole('term').allInnerTexts();
    const definitions = await main.getByRole('definition').allInnerTexts();
    const result: Record<string, string> = {};
    terms.forEach((term, index) => {
      result[term.trim()] = (definitions[index] ?? '').replace(/\s+/g, ' ').trim();
    });
    return result;
  }
}
