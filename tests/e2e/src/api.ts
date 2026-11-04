import type { APIRequestContext } from '@playwright/test';
import { notImplemented } from './notImplemented';

/** Decision body subset used by the specs. */
export interface DecisionBody {
  outcome: 'approved' | 'declined';
  reason: string;
  transactionId: string | null;
  limits: { category: string; period: string; limit: string; remaining: string }[];
}

/** One authorisation request (processor endpoint; card number from seed.ts, never typed into the UI). */
export interface AuthoriseInput {
  cardNumber: string;
  amount: string;
  category: string;
  merchant: string;
  timestamp: string;
}

/**
 * API helpers for arranging E2E state (design §15.2). Implemented in PR-10.
 * - reset(): POST /api/test/reset, expects 204 (local project only).
 * - cardId(name): id of a seeded card from GET /api/cards.
 * - setLimit / authorise: arrange limits and transactions through the real endpoints.
 */
export class Api {
  private readonly request: APIRequestContext;

  constructor(request: APIRequestContext) {
    this.request = request;
  }

  reset(): Promise<void> {
    return notImplemented('PR-10', this.request);
  }

  cardId(name: string): Promise<string> {
    return notImplemented('PR-10', name);
  }

  setLimit(cardId: string, category: string, period: string, amount: string): Promise<void> {
    return notImplemented('PR-10', cardId, category, period, amount);
  }

  authorise(input: AuthoriseInput): Promise<{ status: number; body: DecisionBody; raw: string }> {
    return notImplemented('PR-10', input);
  }
}
