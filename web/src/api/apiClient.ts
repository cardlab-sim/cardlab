import type {
  ActivityEntry,
  AuditEntry,
  CardDetail,
  CardStatus,
  CardSummary,
  CardholderView,
  Category,
  Decision,
  FakeBank,
  Limit,
  PaymentListItem,
  PaymentRecord,
  Period,
  ProblemDetails,
  Quote,
  Receipt,
  SimulatePurchaseInput,
  SteeringInput,
} from './types';

/**
 * The SPA's port to the API (design §5, DEC-20): one method per admin endpoint of §7, plus the 002 cardholder,
 * fake-bank and payment-record endpoints (002 design §5 web/src/api).
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
  /** `GET /api/cards/{id}/audit`, newest first. */
  listAudit(id: string): Promise<AuditEntry[]>;
  /**
   * `POST /api/cards/{id}/simulate-purchase`.
   * @returns The decision body for 200, 400 (invalid_request with problems) and 404 (card_not_found).
   * @throws ApiError for any other status.
   */
  simulatePurchase(id: string, input: SimulatePurchaseInput): Promise<Decision>;

  // ---- 002 full balance payment (design §7). Implemented by HttpApiClient and FakeApiClient in PR-03.

  /**
   * `GET /api/cardholder/cards/{id}` (pure read; the SPA then calls checkPayment when a payment is pending, DEC-13).
   * @throws ApiError 404 for an unknown card.
   */
  getCardholderView(id: string): Promise<CardholderView>;
  /**
   * `POST /api/cardholder/cards/{id}/quotes`: mints the reference shown on the review screen.
   * @throws ApiError 409 with `problem.code` nothing_to_pay | above_limit | payment_pending and `problem.message`.
   */
  createQuote(id: string): Promise<Quote>;
  /**
   * `POST /api/cardholder/cards/{id}/payments/{reference}/confirm`.
   * @throws ApiError 409 with `problem.code` balance_changed | quote_refused | payment_pending and `problem.message`; 404.
   */
  confirmPayment(id: string, reference: string): Promise<Receipt>;
  /** `POST /api/cardholder/cards/{id}/payments/{reference}/check` (W-10 message when unanswered). */
  checkPayment(id: string, reference: string): Promise<Receipt>;
  /** `GET /api/cardholder/cards/{id}/payments/{reference}` (receipt reload). */
  getReceipt(id: string, reference: string): Promise<Receipt>;
  /** `GET /api/fake-bank`. */
  getFakeBank(): Promise<FakeBank>;
  /**
   * `PUT /api/fake-bank/steering`; absent fields keep their value.
   * @throws ApiError 400 for an unknown value.
   */
  setFakeBank(steering: SteeringInput): Promise<FakeBank>;
  /** `GET /api/cards/{id}/activity`: purchases and payments, newest first. */
  listActivity(id: string): Promise<ActivityEntry[]>;
  /** `GET /api/cards/{id}/payments`: every reference, newest first. */
  listPaymentRecords(id: string): Promise<PaymentListItem[]>;
  /**
   * `GET /api/cards/{id}/payments/{reference}` (AC-20).
   * @throws ApiError 404 for an unknown or another card's reference.
   */
  getPaymentRecord(id: string, reference: string): Promise<PaymentRecord>;
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
