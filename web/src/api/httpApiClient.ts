import { ApiError, type ApiClient } from './apiClient';
import type {
  ActivityEntry,
  AuditEntry,
  CardDetail,
  CardholderView,
  CardStatus,
  CardSummary,
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
  Transaction,
} from './types';

/**
 * fetch-based ApiClient (DEC-20). JSON in/out; `Accept: application/json`; ids and path segments URI-encoded.
 * Non-2xx responses reject with ApiError carrying the ProblemDetails body (if JSON), except simulatePurchase,
 * which resolves with the decision body for 200, 400 and 404. A 400 or 404 whose body is not a decision (no
 * string `reason`, e.g. a ProblemDetails or an empty body) rejects with ApiError instead.
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
    if (response.status === 200) {
      return (await response.json()) as Decision;
    }
    if (response.status === 400 || response.status === 404) {
      let body: unknown = null;
      try {
        body = await response.json();
      } catch {
        body = null;
      }
      if (typeof (body as { reason?: unknown } | null)?.reason === 'string') {
        return body as Decision;
      }
      throw new ApiError(
        response.status,
        typeof body === 'object' ? (body as ProblemDetails | null) : null,
      );
    }
    throw await this.toError(response);
  }

  // ---- 002 full balance payment (design §7)

  getCardholderView(id: string): Promise<CardholderView> {
    return this.json<CardholderView>('GET', this.cardholderPath(id));
  }

  createQuote(id: string): Promise<Quote> {
    return this.json<Quote>('POST', `${this.cardholderPath(id)}/quotes`);
  }

  confirmPayment(id: string, reference: string): Promise<Receipt> {
    return this.json<Receipt>('POST', `${this.receiptPath(id, reference)}/confirm`);
  }

  checkPayment(id: string, reference: string): Promise<Receipt> {
    return this.json<Receipt>('POST', `${this.receiptPath(id, reference)}/check`);
  }

  getReceipt(id: string, reference: string): Promise<Receipt> {
    return this.json<Receipt>('GET', this.receiptPath(id, reference));
  }

  getFakeBank(): Promise<FakeBank> {
    return this.json<FakeBank>('GET', '/api/fake-bank');
  }

  setFakeBank(steering: SteeringInput): Promise<FakeBank> {
    return this.json<FakeBank>('PUT', '/api/fake-bank/steering', steering);
  }

  listActivity(id: string): Promise<ActivityEntry[]> {
    return this.json<ActivityEntry[]>('GET', `${this.cardPath(id)}/activity`);
  }

  listPaymentRecords(id: string): Promise<PaymentListItem[]> {
    return this.json<PaymentListItem[]>('GET', `${this.cardPath(id)}/payments`);
  }

  getPaymentRecord(id: string, reference: string): Promise<PaymentRecord> {
    return this.json<PaymentRecord>(
      'GET',
      `${this.cardPath(id)}/payments/${encodeURIComponent(reference)}`,
    );
  }

  private cardholderPath(id: string): string {
    return `/api/cardholder/cards/${encodeURIComponent(id)}`;
  }

  private receiptPath(id: string, reference: string): string {
    return `${this.cardholderPath(id)}/payments/${encodeURIComponent(reference)}`;
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
