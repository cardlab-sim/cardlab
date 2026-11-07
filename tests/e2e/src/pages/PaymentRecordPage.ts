import { expect, type Page } from '@playwright/test';

/** 002 page object for the admin record `/cards/:id/payments/:reference`. */
export class PaymentRecordPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async goto(cardId: string, reference: string): Promise<void> {
    await this.page.goto(`/cards/${cardId}/payments/${reference}`);
    await expect(this.page.getByRole('region', { name: 'What the cardholder saw' })).toBeVisible();
  }

  /** "What the cardholder saw" lines verbatim, plus the version ("v1"). */
  async shown(): Promise<{ lines: string[]; version: string }> {
    const region = this.page.getByRole('region', { name: 'What the cardholder saw' });
    const lines = (await region.getByRole('listitem').allInnerTexts()).map((t) => t.trim());
    const version = /Wording version (\S+)/.exec(await region.innerText())?.[1] ?? '';
    return { lines, version };
  }

  /** Whether the Payment section says "Simulated outcome". */
  async simulated(): Promise<boolean> {
    const region = this.page.getByRole('region', { name: 'Payment', exact: true });
    return (await region.getByText('Simulated outcome').count()) > 0;
  }

  /** The origin badge text in the Acceptance section ("Cardholder view" / "origin: smoke"). */
  async origin(): Promise<string> {
    const region = this.page.getByRole('region', { name: 'Acceptance' });
    const term = region.getByRole('term').filter({ hasText: /^Origin$/ });
    await expect(term).toBeVisible();
    const terms = await region.getByRole('term').allInnerTexts();
    const definitions = await region.getByRole('definition').allInnerTexts();
    return (definitions[terms.indexOf('Origin')] ?? '').trim();
  }

  /** The text of a named section, e.g. "Acceptance" or "Payment". */
  async section(name: string): Promise<string> {
    // First match: the Events table's scroll container is also a region of the same name.
    return (await this.page.getByRole('region', { name, exact: true }).first().innerText()).trim();
  }
}
