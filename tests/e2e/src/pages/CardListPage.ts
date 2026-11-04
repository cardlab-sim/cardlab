import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

/** Page object for `/` (role/label locators only). Implemented in PR-10. */
export class CardListPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** Navigates to `/` and waits for the cards table. */
  goto(): Promise<void> {
    return notImplemented('PR-10');
  }

  /** Status badge text of a card row ("Active" / "Frozen"). */
  statusOf(cardName: string): Promise<string> {
    return notImplemented('PR-10', cardName);
  }

  /** Follows "Open <card name>". */
  open(cardName: string): Promise<void> {
    return notImplemented('PR-10', cardName);
  }
}
