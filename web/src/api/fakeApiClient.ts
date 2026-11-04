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

/** Initial state of a FakeApiClient. */
export interface FakeSeed {
  cards: CardDetail[];
  transactions?: Record<string, Transaction[]>;
  audit?: Record<string, AuditEntry[]>;
}

/**
 * In-memory ApiClient for Vitest (design §15.2). Behaves like the real API for the cases the UI needs:
 * freeze/unfreeze update status and prepend an audit entry (no-ops add none); setLimit validates the amount
 * like R-11 (rejects with ApiError 400 and `problem.errors.amount`) and upserts; removeLimit removes;
 * simulatePurchase returns `nextDecision` if set (else approved), and for outcomes other than invalid_request /
 * card_not_found prepends a transaction. Unknown ids reject with ApiError 404. `calls` records method names.
 * Every method can be made to hang with `hold()` to test pending states.
 * Implemented in PR-02.
 */
export class FakeApiClient implements ApiClient {
  /** Method names in call order, e.g. ['getCard', 'freeze']. */
  readonly calls: string[] = [];

  /** The decision the next simulatePurchase returns (cleared after use). */
  nextDecision: Decision | null = null;

  constructor(seed?: FakeSeed) {
    notImplemented('PR-02', seed);
  }

  /** Makes every subsequent call stay pending until the returned release function is called. */
  hold(): () => void {
    return notImplemented('PR-02');
  }

  listCards(): Promise<CardSummary[]> {
    return notImplemented('PR-02');
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
