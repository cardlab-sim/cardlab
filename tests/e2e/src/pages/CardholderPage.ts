import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

/** A recent-activity row as text. */
export interface ActivityRow {
  date: string;
  description: string;
  amount: string;
  type: string;
  state: string;
}

/**
 * 002 page object for `/cardholder/:id` (role/label locators only). Implemented in PR-08 against the accessible names
 * fixed by PR-03: button "Pay full balance", button "Check status", regions "Balance", "Linked current account",
 * "Recent card activity", "Current account recent activity", "Fake bank (simulation control)" with selects
 * "Next debit outcome" / "Next status check" and button "Set".
 */
export class CardholderPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async goto(cardId: string): Promise<void> {
    notImplemented('PR-08', cardId);
  }

  async balance(): Promise<string> {
    return notImplemented('PR-08');
  }

  async pay(): Promise<void> {
    notImplemented('PR-08');
  }

  async checkStatus(): Promise<void> {
    notImplemented('PR-08');
  }

  async readActivity(): Promise<ActivityRow[]> {
    return notImplemented('PR-08');
  }

  async setFakeBank(outcomeLabel: string, statusCheckLabel?: string): Promise<void> {
    notImplemented('PR-08', outcomeLabel, statusCheckLabel);
  }
}
