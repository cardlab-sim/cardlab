import { ApiError, type ApiClient } from './apiClient';
import type {
  AuditAction,
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

const AMOUNT_PATTERN = /^\d+(\.\d{1,2})?$/;
const MAX_AMOUNT = 1000000;

function pascal(wire: string): string {
  return wire
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join('');
}

/** Audit value of a limit in the API's format (design §6): `Fuel/Day/50.00`, `MealsAndEntertainment/Week/10.00`. */
function limitValue(category: Category, period: Period, amount: string): string {
  return `${pascal(category)}/${pascal(period)}/${amount}`;
}

/**
 * In-memory ApiClient for Vitest (design §15.2). Behaves like the real API for the cases the UI needs:
 * freeze/unfreeze update status and prepend an audit entry (no-ops add none); audit values use the API's format
 * (design §6): `Active`/`Frozen` and `Category/Period/Amount` (PascalCase, e.g. `Fuel/Day/50.00`); setLimit validates the amount
 * like R-11 (rejects with ApiError 400 and `problem.errors.amount`) and upserts; removeLimit removes;
 * simulatePurchase returns `nextDecision` if set (else approved), and for outcomes other than invalid_request /
 * card_not_found prepends a transaction. Unknown ids reject with ApiError 404, except simulatePurchase, which
 * resolves with a card_not_found decision like the API. Audit `atUtc` and the default `occurredAt` come from
 * `Date` (pin with `vi.setSystemTime`). `calls` records method names.
 * Every method can be made to hang with `hold()` to test pending states. Results are copies, so callers
 * cannot mutate the fake's state.
 */
export class FakeApiClient implements ApiClient {
  /** Method names in call order, e.g. ['getCard', 'freeze']. */
  readonly calls: string[] = [];

  /** The decision the next simulatePurchase returns (cleared after use). */
  nextDecision: Decision | null = null;

  private readonly cards: Map<string, CardDetail>;
  private readonly transactions: Map<string, Transaction[]>;
  private readonly audit: Map<string, AuditEntry[]>;
  private gate: Promise<void> | null = null;
  private nextId = 1;

  constructor(seed: FakeSeed = { cards: [] }) {
    this.cards = new Map(seed.cards.map((card) => [card.id, structuredClone(card)]));
    this.transactions = new Map(
      Object.entries(seed.transactions ?? {}).map(([id, list]) => [id, structuredClone(list)]),
    );
    this.audit = new Map(
      Object.entries(seed.audit ?? {}).map(([id, list]) => [id, structuredClone(list)]),
    );
  }

  /** Makes every subsequent call stay pending until the returned release function is called. */
  hold(): () => void {
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    this.gate = gate;
    return () => {
      release();
      if (this.gate === gate) {
        this.gate = null;
      }
    };
  }

  async listCards(): Promise<CardSummary[]> {
    await this.enter('listCards');
    return [...this.cards.values()].map(
      ({ id, name, cardholderName, lastFour, status }): CardSummary => ({
        id,
        name,
        cardholderName,
        lastFour,
        status,
      }),
    );
  }

  async getCard(id: string): Promise<CardDetail> {
    await this.enter('getCard');
    return structuredClone(this.find(id));
  }

  async freeze(id: string): Promise<{ status: CardStatus }> {
    await this.enter('freeze');
    return this.changeStatus(id, 'frozen', 'freeze');
  }

  async unfreeze(id: string): Promise<{ status: CardStatus }> {
    await this.enter('unfreeze');
    return this.changeStatus(id, 'active', 'unfreeze');
  }

  async setLimit(id: string, category: Category, period: Period, amount: string): Promise<Limit> {
    await this.enter('setLimit');
    const card = this.find(id);
    if (!AMOUNT_PATTERN.test(amount) || Number(amount) > MAX_AMOUNT) {
      throw new ApiError(400, {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          amount: ['Amount must be between 0.00 and 1000000.00 with at most two decimals.'],
        },
      });
    }
    const existing = card.limits.find((l) => l.category === category && l.period === period);
    const limit: Limit = { category, period, amount };
    if (existing === undefined) {
      card.limits.push(limit);
      this.addAudit(id, 'limit_set', null, limitValue(category, period, amount));
    } else {
      const old = existing.amount;
      existing.amount = amount;
      this.addAudit(
        id,
        'limit_changed',
        limitValue(category, period, old),
        limitValue(category, period, amount),
      );
    }
    return { ...limit };
  }

  async removeLimit(id: string, category: Category, period: Period): Promise<void> {
    await this.enter('removeLimit');
    const card = this.find(id);
    const index = card.limits.findIndex((l) => l.category === category && l.period === period);
    if (index >= 0) {
      const [removed] = card.limits.splice(index, 1);
      this.addAudit(
        id,
        'limit_removed',
        removed === undefined ? null : limitValue(category, period, removed.amount),
        null,
      );
    }
  }

  async listTransactions(id: string): Promise<Transaction[]> {
    await this.enter('listTransactions');
    this.find(id);
    return structuredClone(this.transactions.get(id) ?? []);
  }

  async listAudit(id: string): Promise<AuditEntry[]> {
    await this.enter('listAudit');
    this.find(id);
    return structuredClone(this.audit.get(id) ?? []);
  }

  async simulatePurchase(id: string, input: SimulatePurchaseInput): Promise<Decision> {
    await this.enter('simulatePurchase');
    if (!this.cards.has(id)) {
      return {
        outcome: 'declined',
        reason: 'card_not_found',
        transactionId: null,
        limits: [],
        problems: [],
      };
    }
    const decision: Decision = this.nextDecision ?? {
      outcome: 'approved',
      reason: 'approved',
      transactionId: null,
      limits: [],
      problems: [],
    };
    this.nextDecision = null;
    if (decision.reason === 'invalid_request' || decision.reason === 'card_not_found') {
      return structuredClone(decision);
    }
    const transactionId = decision.transactionId ?? `fake-tx-${this.nextId++}`;
    const transaction: Transaction = {
      id: transactionId,
      occurredAt: input.timestamp ?? new Date().toISOString(),
      amount: input.amount,
      category: input.category,
      merchant: input.merchant,
      outcome: decision.outcome,
      reason: decision.reason,
    };
    this.transactions.set(id, [transaction, ...(this.transactions.get(id) ?? [])]);
    return { ...structuredClone(decision), transactionId };
  }

  private async enter(method: string): Promise<void> {
    this.calls.push(method);
    if (this.gate !== null) {
      await this.gate;
    }
  }

  private find(id: string): CardDetail {
    const card = this.cards.get(id);
    if (card === undefined) {
      throw new ApiError(404, { title: 'Not Found', status: 404 });
    }
    return card;
  }

  private changeStatus(
    id: string,
    status: CardStatus,
    action: AuditAction,
  ): { status: CardStatus } {
    const card = this.find(id);
    if (card.status !== status) {
      const old = card.status;
      card.status = status;
      this.addAudit(id, action, pascal(old), pascal(status));
    }
    return { status: card.status };
  }

  private addAudit(
    id: string,
    action: AuditAction,
    oldValue: string | null,
    newValue: string | null,
  ): void {
    const entry: AuditEntry = {
      id: `fake-audit-${this.nextId++}`,
      atUtc: new Date().toISOString(),
      actorName: 'Sam Ellis',
      action,
      oldValue,
      newValue,
    };
    this.audit.set(id, [entry, ...(this.audit.get(id) ?? [])]);
  }
}
