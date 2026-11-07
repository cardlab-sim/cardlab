import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

/** 002 page object for `/cardholder/:id/payments/:reference`. Implemented in PR-08. */
export class ReceiptPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** The outcome badge text (Completed / Failed / Pending). */
  async state(): Promise<string> {
    return notImplemented('PR-08');
  }

  /** The outcome message (W-2..W-5, W-10) as shown. */
  async message(): Promise<string> {
    return notImplemented('PR-08');
  }

  /** The `<dl>` fields by term (Reference, Amount, Fee, Time, From, To, Outcome). */
  async fields(): Promise<Record<string, string>> {
    return notImplemented('PR-08');
  }
}
