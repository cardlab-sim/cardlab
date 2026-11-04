import { notImplemented } from '../notImplemented';
import type { ApiClient } from './apiClient';
import type {
  AuditEntry,
  CardDetail,
  CardStatus,
  CardSummary,
  Category,
  Decision,
  Limit,
  Period,
  SimulatePurchaseInput,
  Transaction,
} from './types';

/**
 * fetch-based ApiClient (DEC-20). JSON in/out; `Accept: application/json`; ids and path segments URI-encoded.
 * Non-2xx responses reject with ApiError carrying the ProblemDetails body (if JSON), except simulatePurchase,
 * which resolves with the decision body for 200, 400 and 404.
 * Implemented in PR-02.
 */
export class HttpApiClient implements ApiClient {
  private readonly baseUrl: string;

  /** @param baseUrl - Prefix for every path; '' for same-origin (default). */
  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  listCards(): Promise<CardSummary[]> {
    return notImplemented('PR-02', this.baseUrl);
  }

  getCard(id: string): Promise<CardDetail> {
    return notImplemented('PR-02', id);
  }

  freeze(id: string): Promise<{ status: CardStatus }> {
    return notImplemented('PR-02', id);
  }

  unfreeze(id: string): Promise<{ status: CardStatus }> {
    return notImplemented('PR-02', id);
  }

  setLimit(id: string, category: Category, period: Period, amount: string): Promise<Limit> {
    return notImplemented('PR-02', id, category, period, amount);
  }

  removeLimit(id: string, category: Category, period: Period): Promise<void> {
    return notImplemented('PR-02', id, category, period);
  }

  listTransactions(id: string): Promise<Transaction[]> {
    return notImplemented('PR-02', id);
  }

  listAudit(id: string): Promise<AuditEntry[]> {
    return notImplemented('PR-02', id);
  }

  simulatePurchase(id: string, input: SimulatePurchaseInput): Promise<Decision> {
    return notImplemented('PR-02', id, input);
  }
}
