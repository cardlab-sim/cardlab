import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

/** 002 page object for `/cardholder/:id/pay` (the one review screen). Implemented in PR-08. */
export class ReviewPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /** The W-1 heading and lines in DOM order. */
  async lines(): Promise<string[]> {
    return notImplemented('PR-08');
  }

  /** Clicks the confirm button (named "Confirm payment of £…"). */
  async confirm(): Promise<void> {
    notImplemented('PR-08');
  }
}
