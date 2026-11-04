import type { Page } from '@playwright/test';
import { notImplemented } from '../notImplemented';

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

/** Page object for `/cards/:id` (role/label locators only). Implemented in PR-10. */
export class CardDetailPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  goto(cardId: string): Promise<void> {
    return notImplemented('PR-10', cardId);
  }

  status(): Promise<string> {
    return notImplemented('PR-10');
  }

  freeze(): Promise<void> {
    return notImplemented('PR-10');
  }

  unfreeze(): Promise<void> {
    return notImplemented('PR-10');
  }

  /** Fills category/period/amount (labels from the UI, e.g. "Fuel", "Day") and saves; waits for the table to update or an inline error. */
  setLimit(categoryLabel: string, periodLabel: string, amount: string): Promise<void> {
    return notImplemented('PR-10', categoryLabel, periodLabel, amount);
  }

  removeLimit(categoryLabel: string, periodLabel: string): Promise<void> {
    return notImplemented('PR-10', categoryLabel, periodLabel);
  }

  /** Limits table rows as [category, period, amount] text. */
  limits(): Promise<string[][]> {
    return notImplemented('PR-10');
  }

  /** Submits the simulate form and returns the result notice text once shown. */
  simulate(amount: string, categoryLabel: string, merchant: string): Promise<string> {
    return notImplemented('PR-10', amount, categoryLabel, merchant);
  }

  transactions(): Promise<TransactionRow[]> {
    return notImplemented('PR-10');
  }

  audit(): Promise<AuditItem[]> {
    return notImplemented('PR-10');
  }

  /** Inline error text under a labelled field, or null. */
  fieldError(label: string): Promise<string | null> {
    return notImplemented('PR-10', label);
  }
}
