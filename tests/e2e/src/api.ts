import { expect, type APIRequestContext } from '@playwright/test';

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
 * API helpers for arranging E2E state (design §15.2).
 * - reset(): POST /api/test/reset, expects 204 (local project only).
 * - cardId(name): id of a seeded card from GET /api/cards.
 * - setLimit / authorise: arrange limits and transactions through the real endpoints.
 */
export class Api {
  private readonly request: APIRequestContext;

  constructor(request: APIRequestContext) {
    this.request = request;
  }

  async reset(): Promise<void> {
    const response = await this.request.post('/api/test/reset');
    expect(response.status(), 'POST /api/test/reset').toBe(204);
  }

  async cardId(name: string): Promise<string> {
    const response = await this.request.get('/api/cards');
    expect(response.ok(), 'GET /api/cards').toBe(true);
    const cards = (await response.json()) as { id: string; name: string }[];
    const card = cards.find((c) => c.name === name);
    if (!card) {
      throw new Error(`No card named "${name}"`);
    }
    return card.id;
  }

  async setLimit(cardId: string, category: string, period: string, amount: string): Promise<void> {
    const response = await this.request.put(`/api/cards/${cardId}/limits/${category}/${period}`, {
      data: { amount },
    });
    expect(response.ok(), `PUT limit ${category}/${period}`).toBe(true);
  }

  async authorise(
    input: AuthoriseInput,
  ): Promise<{ status: number; body: DecisionBody; raw: string }> {
    const response = await this.request.post('/api/authorisations', { data: input });
    const raw = await response.text();
    return { status: response.status(), body: JSON.parse(raw) as DecisionBody, raw };
  }
}
