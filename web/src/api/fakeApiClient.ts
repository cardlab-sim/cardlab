import { formatMoney } from '../format/money';
import { ApiError, type ApiClient } from './apiClient';
import type {
  ActivityEntry,
  AuditAction,
  AuditEntry,
  CardDetail,
  CardholderView,
  CardStatus,
  CardSummary,
  Category,
  Decision,
  AccountEntry,
  FailureReason,
  FakeBank,
  Limit,
  NextDebitOutcome,
  NextStatusCheck,
  PaymentListItem,
  PaymentOrigin,
  PaymentProblemCode,
  PaymentRecord,
  PaymentState,
  Period,
  ProblemDetails,
  Quote,
  QuoteStatus,
  Receipt,
  SimulatePurchaseInput,
  SteeringInput,
  Transaction,
} from './types';

/** Initial state of a FakeApiClient. */
export interface FakeSeed {
  cards: CardDetail[];
  transactions?: Record<string, Transaction[]>;
  audit?: Record<string, AuditEntry[]>;
  /** 002: purchase amounts per card id, newest first (added to the card's seeded `balanceOwed`). */
  purchases?: Record<string, string[]>;
  /** 002: the linked current account (defaults: "Brightline Supplies Ltd current account", 7731, 25000.00). */
  account?: { displayName?: string; lastFour?: string; availableBalance?: string };
}

/** One payment attempt: the quote and, once confirmed, the payment. */
interface PaymentRec {
  reference: string;
  cardId: string;
  cents: number;
  quote: Quote;
  quoteStatus: QuoteStatus;
  refusal: { balanceCents: number } | null;
  simulated: boolean;
  events: PaymentRecord['events'];
  state: PaymentState | null;
  failureReason: FailureReason | null;
  createdAt: string;
  effectiveAt: string | null;
  debitedAt: string | null;
}

interface PurchaseItem {
  kind: 'purchase';
  /** The shown time: the processor timestamp (`occurredAt`). */
  at: string;
  /** When the purchase was decided: the ordering time. */
  decidedAt: string;
  cents: number;
  description: string;
  category: Category;
  outcome: Decision['outcome'];
  reason: Decision['reason'];
}

type ActivityItem = PurchaseItem | { kind: 'payment'; reference: string; at: string };

/** Ordering time of an activity row: decision time for purchases, confirm time for payments. */
const placedAt = (item: ActivityItem): number =>
  Date.parse(item.kind === 'purchase' ? item.decidedAt : item.at);

const MAX_PAYABLE_CENTS = 100000000;
const AUTHORITY_BASIS =
  'Standing authority from Brightline Supplies Ltd to pay this card from this account.';
const WHEN_LINE =
  'When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.';

function toCents(amount: string): number {
  const [whole = '0', fraction = ''] = amount.split('.');
  return Number(whole) * 100 + Number(fraction.padEnd(2, '0'));
}

function fromCents(cents: number): string {
  return `${Math.floor(cents / 100)}.${String(cents % 100).padStart(2, '0')}`;
}

function conflict(code: PaymentProblemCode, message: string, extra: ProblemDetails = {}): ApiError {
  return new ApiError(409, { title: 'Conflict', status: 409, code, message, ...extra });
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
 * 002: simulates the cardholder payment flow (quote, confirm, check, receipt) with the §17 wording, a one-shot next debit
 * outcome (`withBank`), the status-check steering (`withStatusCheck`) and `addPurchase`; balance owed = approved purchases minus
 * completed payments (R-1); activity lists approved and declined purchases (shown at the processor time, ordered by decision time) and payments (by confirm time) newest first, like design §5 erratum; `withQuoteOrigin` sets the next quote's origin. Each payment record carries the events of its attempts
 * and `simulated` (true when a fake-bank steering value produced its outcome); steering is one-shot (DEC-6, DEC-7).
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
  private nextReference = 1;
  private readonly activity = new Map<string, ActivityItem[]>();
  private readonly payments = new Map<string, PaymentRec>();
  private readonly ledger: AccountEntry[] = [];
  private readonly account: { displayName: string; lastFour: string; balanceCents: number };
  private nextDebitOutcome: NextDebitOutcome = 'complete';
  private nextStatusCheck: NextStatusCheck = 'answers';
  private debitSteered = false;
  private checkSteered = false;
  private nextQuoteOrigin: PaymentOrigin = 'cardholder_view';

  constructor(seed: FakeSeed = { cards: [] }) {
    this.cards = new Map(seed.cards.map((card) => [card.id, structuredClone(card)]));
    this.transactions = new Map(
      Object.entries(seed.transactions ?? {}).map(([id, list]) => [id, structuredClone(list)]),
    );
    this.audit = new Map(
      Object.entries(seed.audit ?? {}).map(([id, list]) => [id, structuredClone(list)]),
    );
    const at = new Date().toISOString();
    for (const [id, card] of this.cards) {
      const opening = toCents(card.balanceOwed);
      const amounts = [
        ...(seed.purchases?.[id] ?? []),
        ...(opening > 0 ? [fromCents(opening)] : []),
      ];
      this.activity.set(
        id,
        amounts.map((amount): ActivityItem => ({
          kind: 'purchase',
          at,
          decidedAt: at,
          cents: toCents(amount),
          description: 'Card purchase',
          category: 'other',
          outcome: 'approved',
          reason: 'approved',
        })),
      );
    }
    this.account = {
      displayName: seed.account?.displayName ?? 'Brightline Supplies Ltd current account',
      lastFour: seed.account?.lastFour ?? '7731',
      balanceCents: toCents(seed.account?.availableBalance ?? '25000.00'),
    };
  }

  /** Sets the next debit outcome (one-shot, same wire values as the fake bank); returns the fake for chaining. */
  withBank(outcome: NextDebitOutcome): this {
    this.nextDebitOutcome = outcome;
    this.debitSteered = true;
    return this;
  }

  /** Sets whether status checks are answered (one-shot like the debit outcome: applies to the next status check that reaches the bank, then reverts to answers); returns the fake for chaining. */
  withStatusCheck(check: NextStatusCheck): this {
    this.nextStatusCheck = check;
    this.checkSteered = true;
    return this;
  }

  /** Adds an approved purchase to the card's activity (raises the balance owed, e.g. between quote and confirm). */
  addPurchase(id: string, amount: string): void {
    this.find(id);
    this.activityOf(id).unshift({
      kind: 'purchase',
      at: new Date().toISOString(),
      decidedAt: new Date().toISOString(),
      cents: toCents(amount),
      description: 'Card purchase',
      category: 'other',
      outcome: 'approved',
      reason: 'approved',
    });
  }

  /** Sets the origin of the next quote (one-shot, e.g. 'deploy_smoke' for DEC-15 badges); returns the fake for chaining. */
  withQuoteOrigin(origin: PaymentOrigin): this {
    this.nextQuoteOrigin = origin;
    return this;
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
    return { ...structuredClone(this.find(id)), balanceOwed: fromCents(this.owed(id)) };
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
    this.activityOf(id).unshift({
      kind: 'purchase',
      at: transaction.occurredAt,
      decidedAt: new Date().toISOString(),
      cents: toCents(input.amount),
      description: input.merchant,
      category: input.category,
      outcome: decision.outcome,
      reason: decision.reason,
    });
    return { ...structuredClone(decision), transactionId };
  }

  // ---- 002 full balance payment

  async getCardholderView(id: string): Promise<CardholderView> {
    await this.enter('getCardholderView');
    return this.view(id);
  }

  async createQuote(id: string): Promise<Quote> {
    await this.enter('createQuote');
    const card = this.find(id);
    const pending = this.pendingOf(id);
    if (pending !== undefined) {
      throw this.pendingConflict(pending);
    }
    const owed = this.owed(id);
    if (owed === 0) {
      throw conflict('nothing_to_pay', this.nothingToPayMessage(), { balanceOwed: '0.00' });
    }
    if (owed > MAX_PAYABLE_CENTS) {
      throw conflict('above_limit', this.aboveLimitMessage(owed), { balanceOwed: fromCents(owed) });
    }
    const amount = fromCents(owed);
    const reference = `PAY-${String(this.nextReference++).padStart(8, '0')}`;
    const to = `${card.name} card, ending ${card.lastFour}`;
    const quote: Quote = {
      reference,
      amount,
      fee: '0.00',
      total: amount,
      from: this.fromText(),
      to,
      wording: {
        version: 'v1',
        heading: 'Pay your full balance',
        lines: [
          `Amount: ${formatMoney(amount)}`,
          'Fee: £0.00 (no fee)',
          `Total to pay: ${formatMoney(amount)}`,
          WHEN_LINE,
          `From: ${this.fromText()}`,
          `To: ${to}`,
        ],
        button: `Confirm payment of ${formatMoney(amount)}`,
      },
      createdAt: new Date().toISOString(),
      origin: this.nextQuoteOrigin,
    };
    this.nextQuoteOrigin = 'cardholder_view';
    this.payments.set(reference, {
      reference,
      cardId: id,
      cents: owed,
      quote,
      quoteStatus: 'open',
      refusal: null,
      simulated: false,
      events: [],
      state: null,
      failureReason: null,
      createdAt: quote.createdAt,
      effectiveAt: null,
      debitedAt: null,
    });
    return structuredClone(quote);
  }

  async confirmPayment(id: string, reference: string): Promise<Receipt> {
    await this.enter('confirmPayment');
    this.find(id);
    const known = this.payments.get(reference);
    if (known?.cardId === id && known.quoteStatus === 'refused' && known.state === null) {
      const balanceAtRefusal = known.refusal?.balanceCents ?? 0;
      this.addEvent(known, 'confirm_refused', null, null, 'quote_refused', false);
      throw conflict('quote_refused', this.balanceChangedMessage(balanceAtRefusal), {
        balanceOwed: fromCents(this.owed(id)),
      });
    }
    const rec = this.findPayment(id, reference);
    if (rec.state !== null) {
      this.addEvent(rec, 'confirm_repeated', rec.state, rec.state, null, false);
      return { ...this.receipt(rec), repeated: true };
    }
    const pending = this.pendingOf(id);
    if (pending !== undefined) {
      throw this.pendingConflict(pending);
    }
    const owed = this.owed(id);
    if (owed !== rec.cents) {
      rec.quoteStatus = 'refused';
      rec.refusal = { balanceCents: owed };
      this.addEvent(rec, 'confirm_refused', null, null, 'balance_changed', false);
      throw conflict('balance_changed', this.balanceChangedMessage(owed), {
        balanceOwed: fromCents(owed),
      });
    }
    rec.quoteStatus = 'confirmed';
    const now = new Date().toISOString();
    const outcome = this.nextDebitOutcome;
    const steered = this.debitSteered;
    this.nextDebitOutcome = 'complete';
    this.debitSteered = false;
    rec.simulated = steered;
    this.activityOf(id).unshift({ kind: 'payment', reference, at: now });
    this.addEvent(rec, 'confirm_created', null, 'pending', null, false);
    this.addEvent(rec, 'debit_requested', null, null, null, false);
    if (outcome === 'complete' && rec.cents > this.account.balanceCents) {
      this.settle(rec, 'failed', now, 'insufficient_funds');
      this.addEvent(rec, 'bank_refused', 'pending', 'failed', 'insufficient_funds', steered);
    } else if (outcome === 'complete') {
      this.debit(rec, now);
      this.settle(rec, 'completed', now, null);
      this.addEvent(rec, 'bank_completed', 'pending', 'completed', null, steered);
    } else if (outcome === 'refuse_insufficient_funds') {
      this.settle(rec, 'failed', now, 'insufficient_funds');
      this.addEvent(rec, 'bank_refused', 'pending', 'failed', 'insufficient_funds', steered);
    } else if (outcome === 'refuse_account_unavailable') {
      this.settle(rec, 'failed', now, 'account_unavailable');
      this.addEvent(rec, 'bank_refused', 'pending', 'failed', 'account_unavailable', steered);
    } else {
      rec.state = 'pending';
      if (outcome === 'timeout_debit_applied') {
        this.debit(rec, now);
      }
      this.addEvent(rec, 'bank_unanswered', 'pending', 'pending', null, steered);
    }
    return this.receipt(rec);
  }

  async checkPayment(id: string, reference: string): Promise<Receipt> {
    await this.enter('checkPayment');
    const rec = this.findPayment(id, reference);
    if (rec.state !== 'pending') {
      return this.receipt(rec);
    }
    const check = this.nextStatusCheck;
    const steered = this.checkSteered;
    this.nextStatusCheck = 'answers';
    this.checkSteered = false;
    rec.simulated = rec.simulated || steered;
    if (check === 'does_not_answer') {
      this.addEvent(rec, 'status_check_unanswered', 'pending', 'pending', null, steered);
      return {
        ...this.receipt(rec),
        message: `Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference ${reference}.`,
      };
    }
    if (rec.debitedAt !== null) {
      this.settle(rec, 'completed', rec.debitedAt, null);
      this.addEvent(rec, 'status_check_completed', 'pending', 'completed', null, steered);
    } else {
      this.settle(rec, 'failed', new Date().toISOString(), 'not_received');
      this.addEvent(rec, 'status_check_not_received', 'pending', 'failed', 'not_received', steered);
    }
    return this.receipt(rec);
  }

  async getReceipt(id: string, reference: string): Promise<Receipt> {
    await this.enter('getReceipt');
    return this.receipt(this.findPayment(id, reference));
  }

  async getFakeBank(): Promise<FakeBank> {
    await this.enter('getFakeBank');
    return this.bank();
  }

  async setFakeBank(steering: SteeringInput): Promise<FakeBank> {
    await this.enter('setFakeBank');
    if (steering.nextDebitOutcome !== undefined) {
      this.withBank(steering.nextDebitOutcome);
    }
    if (steering.nextStatusCheck !== undefined) {
      this.withStatusCheck(steering.nextStatusCheck);
    }
    return this.bank();
  }

  async listActivity(id: string): Promise<ActivityEntry[]> {
    await this.enter('listActivity');
    this.find(id);
    return this.activityEntries(id);
  }

  async listPaymentRecords(id: string): Promise<PaymentListItem[]> {
    await this.enter('listPaymentRecords');
    this.find(id);
    return [...this.payments.values()]
      .filter((rec) => rec.cardId === id)
      .reverse()
      .map((rec): PaymentListItem => ({
        reference: rec.reference,
        status: rec.quoteStatus,
        state: rec.state,
        amount: fromCents(rec.cents),
        createdAt: rec.createdAt,
        settledAt: rec.state === 'pending' ? null : rec.effectiveAt,
        failureReason: rec.failureReason,
        origin: rec.quote.origin,
      }));
  }

  async getPaymentRecord(id: string, reference: string): Promise<PaymentRecord> {
    await this.enter('getPaymentRecord');
    const card = this.find(id);
    const rec = this.findPayment(id, reference, true);
    const q = rec.quote;
    return {
      quote: {
        reference,
        kind: 'full',
        amount: q.amount,
        fee: q.fee,
        total: q.total,
        from: q.from,
        to: q.to,
        wording: structuredClone(q.wording),
        createdAt: q.createdAt,
      },
      status: rec.quoteStatus,
      refusalReason: rec.refusal === null ? null : 'balance_changed',
      balanceAtRefusal: rec.refusal === null ? null : fromCents(rec.refusal.balanceCents),
      acceptance: {
        at: rec.state === null ? null : rec.createdAt,
        actor: { kind: 'cardholder', name: card.cardholderName },
        authorityBasis: AUTHORITY_BASIS,
        origin: q.origin,
      },
      payment:
        rec.state === null
          ? null
          : {
              state: rec.state,
              amount: fromCents(rec.cents),
              fee: '0.00',
              createdAt: rec.createdAt,
              settledAt: rec.state === 'pending' ? null : rec.effectiveAt,
              effectiveAt: rec.state === 'completed' ? rec.effectiveAt : null,
              bankDebitedAt: rec.debitedAt,
              failureReason: rec.failureReason,
              balanceBefore: rec.state === 'pending' ? null : fromCents(rec.cents),
              balanceAfter:
                rec.state === 'pending'
                  ? null
                  : rec.state === 'completed'
                    ? '0.00'
                    : fromCents(rec.cents),
              simulated: rec.simulated,
            },
      events: structuredClone(rec.events),
    };
  }

  private activityOf(id: string): ActivityItem[] {
    let list = this.activity.get(id);
    if (list === undefined) {
      list = [];
      this.activity.set(id, list);
    }
    return list;
  }

  private owed(id: string): number {
    const total = this.activityOf(id).reduce((sum, item) => {
      if (item.kind === 'purchase') {
        return item.outcome === 'approved' ? sum + item.cents : sum;
      }
      const rec = this.payments.get(item.reference);
      return rec?.state === 'completed' ? sum - rec.cents : sum;
    }, 0);
    return Math.max(0, total);
  }

  private pendingOf(id: string): PaymentRec | undefined {
    return [...this.payments.values()].find((rec) => rec.cardId === id && rec.state === 'pending');
  }

  private pendingConflict(pending: PaymentRec): ApiError {
    return conflict('payment_pending', this.pendingMessage(pending), {
      amount: fromCents(pending.cents),
      pendingReference: pending.reference,
      balanceOwed: fromCents(this.owed(pending.cardId)),
    });
  }

  private findPayment(id: string, reference: string, allowRefused = false): PaymentRec {
    this.find(id);
    const rec = this.payments.get(reference);
    if (rec?.cardId !== id || (!allowRefused && rec.state === null && rec.quoteStatus !== 'open')) {
      throw new ApiError(404, { title: 'Not Found', status: 404 });
    }
    return rec;
  }

  private balanceChangedMessage(balanceCents: number): string {
    return `Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now ${formatMoney(fromCents(balanceCents))}. Review the new amount and confirm again if you want to pay it.`;
  }

  private addEvent(
    rec: PaymentRec,
    kind: string,
    from: PaymentState | null,
    to: PaymentState | null,
    detail: string | null,
    simulated: boolean,
  ): void {
    rec.events.push({ at: new Date().toISOString(), kind, from, to, detail, simulated });
  }

  private fromText(): string {
    return `${this.account.displayName}, ending ${this.account.lastFour}`;
  }

  private debit(rec: PaymentRec, at: string): void {
    this.account.balanceCents -= rec.cents;
    rec.debitedAt = at;
    this.ledger.unshift({
      reference: rec.reference,
      amount: fromCents(rec.cents),
      postedAt: at,
      narrative: `Card payment, ${rec.quote.to}`,
    });
  }

  private settle(
    rec: PaymentRec,
    state: PaymentState,
    at: string,
    reason: FailureReason | null,
  ): void {
    rec.state = state;
    rec.effectiveAt = at;
    rec.failureReason = reason;
  }

  private pendingMessage(rec: PaymentRec): string {
    return `A payment of ${formatMoney(fromCents(rec.cents))} (reference ${rec.reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.`;
  }

  private nothingToPayMessage(): string {
    return 'Nothing to pay. Your card balance is £0.00.';
  }

  private aboveLimitMessage(owed: number): string {
    return `This balance cannot be paid here. Payments above £1,000,000.00 are not available online. No money has moved. Your card balance is ${formatMoney(fromCents(owed))}. Contact your account administrator.`;
  }

  private receipt(rec: PaymentRec): Receipt {
    const balance = formatMoney(fromCents(this.owed(rec.cardId)));
    const ref = rec.reference;
    let message: string;
    if (rec.state === 'completed') {
      message = `Payment complete. ${formatMoney(fromCents(rec.cents))} has left your current account and paid your card. Your card balance is now ${balance}. Reference ${ref}.`;
    } else if (rec.state === 'pending') {
      message = `Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows ${balance} until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference ${ref}.`;
    } else if (rec.failureReason === 'insufficient_funds') {
      message = `Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still ${balance}. Add money to the account, then try again. Reference ${ref}.`;
    } else {
      message = `Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still ${balance}. Try again later. Reference ${ref}.`;
    }
    return {
      reference: ref,
      state: rec.state ?? 'pending',
      amount: fromCents(rec.cents),
      fee: '0.00',
      time: rec.effectiveAt ?? rec.createdAt,
      from: rec.quote.from,
      to: rec.quote.to,
      failureReason: rec.failureReason,
      message,
      balanceOwed: fromCents(this.owed(rec.cardId)),
      repeated: false,
      origin: rec.quote.origin,
    };
  }

  private activityEntries(id: string): ActivityEntry[] {
    const items = this.activityOf(id);
    const entries = items.map((item): ActivityEntry => {
      if (item.kind === 'purchase') {
        return {
          at: item.at,
          kind: 'purchase',
          amount: fromCents(item.cents),
          description: item.description,
          outcome: item.outcome,
          state: null,
          reference: null,
          origin: null,
          category: item.category,
          reason: item.reason,
        };
      }
      const rec = this.payments.get(item.reference);
      return {
        at: item.at,
        kind: 'payment',
        amount: fromCents(rec?.cents ?? 0),
        description: `Payment ${item.reference}`,
        outcome: null,
        state: rec?.state ?? 'pending',
        reference: item.reference,
        origin: rec?.quote.origin ?? 'cardholder_view',
        category: null,
        reason: null,
      };
    });
    return entries
      .map((entry, i) => ({ entry, key: placedAt(items[i]!) }))
      .sort((a, b) => b.key - a.key)
      .map((x) => x.entry);
  }

  private view(id: string): CardholderView {
    const card = this.find(id);
    const owed = this.owed(id);
    const pending = this.pendingOf(id);
    let availability: CardholderView['availability'] = 'available';
    let availabilityMessage: string | null = null;
    if (pending !== undefined) {
      availability = 'payment_pending';
      availabilityMessage = this.pendingMessage(pending);
    } else if (owed === 0) {
      availability = 'nothing_to_pay';
      availabilityMessage = this.nothingToPayMessage();
    } else if (owed > MAX_PAYABLE_CENTS) {
      availability = 'above_limit';
      availabilityMessage = this.aboveLimitMessage(owed);
    }
    return {
      card: {
        id: card.id,
        name: card.name,
        lastFour: card.lastFour,
        cardholderName: card.cardholderName,
        status: card.status,
      },
      balanceOwed: fromCents(owed),
      availability,
      availabilityMessage,
      pendingPayment: pending === undefined ? null : this.receipt(pending),
      account: {
        displayName: this.account.displayName,
        lastFour: this.account.lastFour,
        availableBalance: fromCents(this.account.balanceCents),
        authorityBasis: AUTHORITY_BASIS,
      },
      cardActivity: this.activityEntries(id).slice(0, 10),
      accountActivity: structuredClone(this.ledger.slice(0, 10)),
    };
  }

  private bank(): FakeBank {
    return {
      nextDebitOutcome: this.nextDebitOutcome,
      nextStatusCheck: this.nextStatusCheck,
      account: {
        lastFour: this.account.lastFour,
        availableBalance: fromCents(this.account.balanceCents),
      },
      entries: structuredClone(this.ledger),
    };
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
