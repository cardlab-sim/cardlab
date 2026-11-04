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
 * The SPA's port to the admin API (design §5, DEC-20): one method per admin endpoint of §7.
 * Health, the processor endpoint and test reset are not part of the SPA.
 * Complete in PR-00; implemented by HttpApiClient and FakeApiClient (PR-02).
 */
export interface ApiClient {
  /** `GET /api/cards`. */
  listCards(): Promise<CardSummary[]>;
  /**
   * `GET /api/cards/{id}`.
   * @throws ApiError with status 404 for an unknown card.
   */
  getCard(id: string): Promise<CardDetail>;
  /** `POST /api/cards/{id}/freeze`; resolves with the status after the call. */
  freeze(id: string): Promise<{ status: CardStatus }>;
  /** `POST /api/cards/{id}/unfreeze`. */
  unfreeze(id: string): Promise<{ status: CardStatus }>;
  /**
   * `PUT /api/cards/{id}/limits/{category}/{period}` with `{amount}`.
   * @throws ApiError with status 400 (and `problem.errors`) for an invalid amount.
   */
  setLimit(id: string, category: Category, period: Period, amount: string): Promise<Limit>;
  /** `DELETE /api/cards/{id}/limits/{category}/{period}` (204). */
  removeLimit(id: string, category: Category, period: Period): Promise<void>;
  /** `GET /api/cards/{id}/transactions`, newest first. */
  listTransactions(id: string): Promise<Transaction[]>;
  /** `GET /api/cards/{id}/audit`, newest first. */
  listAudit(id: string): Promise<AuditEntry[]>;
  /**
   * `POST /api/cards/{id}/simulate-purchase`.
   * @returns The decision body for 200, 400 (invalid_request with problems) and 404 (card_not_found).
   * @throws ApiError for any other status.
   */
  simulatePurchase(id: string, input: SimulatePurchaseInput): Promise<Decision>;
}

/** A failed API call: HTTP status plus the ProblemDetails body when there was one. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null, message?: string) {
    super(message ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}
