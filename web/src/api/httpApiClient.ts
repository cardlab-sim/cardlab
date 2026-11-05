import { ApiError, type ApiClient } from './apiClient';
import type {
  AuditEntry,
  CardDetail,
  CardStatus,
  CardSummary,
  Category,
  Decision,
  Limit,
  Period,
  ProblemDetails,
  SimulatePurchaseInput,
  Transaction,
} from './types';

/**
 * fetch-based ApiClient (DEC-20). JSON in/out; `Accept: application/json`; ids and path segments URI-encoded.
 * Non-2xx responses reject with ApiError carrying the ProblemDetails body (if JSON), except simulatePurchase,
 * which resolves with the decision body for 200, 400 and 404.
 */
export class HttpApiClient implements ApiClient {
  private readonly baseUrl: string;

  /** @param baseUrl - Prefix for every path; '' for same-origin (default). */
  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  listCards(): Promise<CardSummary[]> {
    return this.json<CardSummary[]>('GET', '/api/cards');
  }

  getCard(id: string): Promise<CardDetail> {
    return this.json<CardDetail>('GET', this.cardPath(id));
  }

  freeze(id: string): Promise<{ status: CardStatus }> {
    return this.json<{ status: CardStatus }>('POST', `${this.cardPath(id)}/freeze`);
  }

  unfreeze(id: string): Promise<{ status: CardStatus }> {
    return this.json<{ status: CardStatus }>('POST', `${this.cardPath(id)}/unfreeze`);
  }

  setLimit(id: string, category: Category, period: Period, amount: string): Promise<Limit> {
    return this.json<Limit>('PUT', this.limitPath(id, category, period), { amount });
  }

  async removeLimit(id: string, category: Category, period: Period): Promise<void> {
    const response = await this.send('DELETE', this.limitPath(id, category, period));
    await this.throwIfFailed(response);
  }

  listTransactions(id: string): Promise<Transaction[]> {
    return this.json<Transaction[]>('GET', `${this.cardPath(id)}/transactions`);
  }

  listAudit(id: string): Promise<AuditEntry[]> {
    return this.json<AuditEntry[]>('GET', `${this.cardPath(id)}/audit`);
  }

  async simulatePurchase(id: string, input: SimulatePurchaseInput): Promise<Decision> {
    const response = await this.send('POST', `${this.cardPath(id)}/simulate-purchase`, input);
    if (response.status === 200 || response.status === 400 || response.status === 404) {
      return (await response.json()) as Decision;
    }
    throw await this.toError(response);
  }

  private cardPath(id: string): string {
    return `/api/cards/${encodeURIComponent(id)}`;
  }

  private limitPath(id: string, category: Category, period: Period): string {
    return `${this.cardPath(id)}/limits/${encodeURIComponent(category)}/${encodeURIComponent(period)}`;
  }

  private send(method: string, path: string, body?: unknown): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    const init: RequestInit = { method, headers };
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(body);
    }
    return fetch(`${this.baseUrl}${path}`, init);
  }

  private async json<T>(method: string, path: string, body?: unknown): Promise<T> {
    const response = await this.send(method, path, body);
    await this.throwIfFailed(response);
    return (await response.json()) as T;
  }

  private async throwIfFailed(response: Response): Promise<void> {
    if (!response.ok) {
      throw await this.toError(response);
    }
  }

  private async toError(response: Response): Promise<ApiError> {
    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      problem = null;
    }
    return new ApiError(response.status, problem);
  }
}
