import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

/** 002 page object for the admin record `/cards/:id/payments/:reference`. Implemented in PR-08. */
export class PaymentRecordPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async goto(cardId: string, reference: string): Promise<void> {
    notImplemented('PR-08', cardId, reference);
  }

  /** "What the cardholder saw" lines verbatim, plus the version text. */
  async shown(): Promise<{ lines: string[]; version: string }> {
    return notImplemented('PR-08');
  }

  /** Whether the record says "Simulated outcome". */
  async simulated(): Promise<boolean> {
    return notImplemented('PR-08');
  }

  /** The origin badge text. */
  async origin(): Promise<string> {
    return notImplemented('PR-08');
  }
}
