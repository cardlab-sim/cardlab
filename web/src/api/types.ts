// Wire types of design §7 (complete in PR-00; shared by every web PR). Amounts are two-decimal strings ("50.00"),
// timestamps ISO-8601 with offset. The SPA never handles a card number (NFR-1, DEC-14).

export type CardStatus = 'active' | 'frozen';

export type Category =
  | 'travel'
  | 'fuel'
  | 'meals_and_entertainment'
  | 'software_and_subscriptions'
  | 'office_supplies'
  | 'advertising'
  | 'professional_services'
  | 'other';

export type Period = 'day' | 'week' | 'month';

export type Outcome = 'approved' | 'declined';

export type Reason =
  'approved' | 'card_frozen' | 'limit_exceeded' | 'card_not_found' | 'invalid_request';

export type AuditAction = 'freeze' | 'unfreeze' | 'limit_set' | 'limit_changed' | 'limit_removed';

/** `GET /api/cards` item. */
export interface CardSummary {
  id: string;
  name: string;
  cardholderName: string;
  lastFour: string;
  status: CardStatus;
}

/** A spend limit. */
export interface Limit {
  category: Category;
  period: Period;
  amount: string;
}

/** `GET /api/cards/{id}`. */
export interface CardDetail extends CardSummary {
  limits: Limit[];
  /** 002 R-1: balance owed, e.g. "150.50" (filled by the API from 002 PR-07). */
  balanceOwed: string;
}

/** `GET /api/cards/{id}/transactions` item (newest first). */
export interface Transaction {
  id: string;
  occurredAt: string;
  amount: string;
  category: Category;
  merchant: string;
  outcome: Outcome;
  reason: Reason;
}

/** `GET /api/cards/{id}/audit` item (newest first). */
export interface AuditEntry {
  id: string;
  atUtc: string;
  actorName: string;
  action: AuditAction;
  oldValue: string | null;
  newValue: string | null;
}

/** Remaining amount of one limit after a decision. */
export interface LimitRemaining {
  category: Category;
  period: Period;
  limit: string;
  remaining: string;
}

/** One validation problem (field: `amount`, `category`, `merchant` or `timestamp` on the simulate path). */
export interface Problem {
  field: string;
  message: string;
}

/** Decision body of the simulate endpoint (200, 400 and 404 all carry it). */
export interface Decision {
  outcome: Outcome;
  reason: Reason;
  transactionId: string | null;
  limits: LimitRemaining[];
  problems: Problem[];
}

/** Body of `POST /api/cards/{id}/simulate-purchase`. */
export interface SimulatePurchaseInput {
  amount: string;
  category: Category;
  merchant: string;
  timestamp?: string;
}

/** RFC 9457 ProblemDetails as returned by the admin endpoints. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  /** 002: payment refusal code on 409s. */
  code?: PaymentProblemCode;
  /** 002: the §17 wording for the refusal, shown verbatim. */
  message?: string;
  /** 002: balance owed now. */
  balanceOwed?: string;
  /** 002: the pending payment's reference (payment_pending). */
  pendingReference?: string;
  /** 002: the pending payment's amount (payment_pending). */
  amount?: string;
}

/** Display labels (English UK) in the order of design R-4. */
export const CATEGORIES: readonly { value: Category; label: string }[] = [
  { value: 'travel', label: 'Travel' },
  { value: 'fuel', label: 'Fuel' },
  { value: 'meals_and_entertainment', label: 'Meals & Entertainment' },
  { value: 'software_and_subscriptions', label: 'Software & Subscriptions' },
  { value: 'office_supplies', label: 'Office Supplies' },
  { value: 'advertising', label: 'Advertising' },
  { value: 'professional_services', label: 'Professional Services' },
  { value: 'other', label: 'Other' },
];

export const PERIODS: readonly { value: Period; label: string }[] = [
  { value: 'day', label: 'Day' },
  { value: 'week', label: 'Week' },
  { value: 'month', label: 'Month' },
];

// ---------------------------------------------------------------------------------------------------------------------
// 002 full balance payment (design §7). Complete in PR-00. Messages (W-1..W-10) come from the server and are shown
// verbatim (DEC-10); the SPA never builds customer-facing payment wording.

export type PaymentState = 'pending' | 'completed' | 'failed';

export type FailureReason = 'insufficient_funds' | 'account_unavailable' | 'not_received';

export type PayAvailability = 'available' | 'nothing_to_pay' | 'above_limit' | 'payment_pending';

export type ActivityKind = 'purchase' | 'payment';

export type PaymentOrigin = 'cardholder_view' | 'deploy_smoke';

export type QuoteStatus = 'open' | 'confirmed' | 'refused';

export type PaymentProblemCode =
  'nothing_to_pay' | 'above_limit' | 'payment_pending' | 'balance_changed' | 'quote_refused';

export type NextDebitOutcome =
  | 'complete'
  | 'refuse_insufficient_funds'
  | 'refuse_account_unavailable'
  | 'timeout_debit_applied'
  | 'timeout_not_received';

export type NextStatusCheck = 'answers' | 'does_not_answer';

/** A card activity row (cardholder view and `GET /api/cards/{id}/activity`), newest first. */
export interface ActivityEntry {
  at: string;
  kind: ActivityKind;
  amount: string;
  /** Merchant for purchases; "Payment PAY-…" for payments. */
  description: string;
  /** Purchases only. */
  outcome: Outcome | null;
  /** Payments only. */
  state: PaymentState | null;
  /** Payments only. */
  reference: string | null;
  /** Payments only. */
  origin: PaymentOrigin | null;
  /** Purchases only. */
  category: Category | null;
  /** Purchases only. */
  reason: Reason | null;
}

/** A current-account ledger entry, newest first. */
export interface AccountEntry {
  reference: string;
  amount: string;
  postedAt: string;
  narrative: string;
}

/** Receipt (confirm, check, receipt reload, and the view's pending payment). */
export interface Receipt {
  reference: string;
  state: PaymentState;
  amount: string;
  fee: string;
  /** Effective time when settled; acceptance time when pending. */
  time: string;
  from: string;
  to: string;
  failureReason: FailureReason | null;
  /** W-2..W-5 or W-10, verbatim. */
  message: string;
  balanceOwed: string;
  repeated: boolean;
  origin: PaymentOrigin;
}

/** `GET /api/cardholder/cards/{id}`. */
export interface CardholderView {
  card: { id: string; name: string; lastFour: string; cardholderName: string; status: CardStatus };
  balanceOwed: string;
  availability: PayAvailability;
  /** W-7 / W-8 / W-9 when not available. */
  availabilityMessage: string | null;
  pendingPayment: Receipt | null;
  account: {
    displayName: string;
    lastFour: string;
    availableBalance: string;
    authorityBasis: string;
  };
  cardActivity: ActivityEntry[];
  accountActivity: AccountEntry[];
}

/** Rendered wording (W-1 on the review screen). */
export interface Wording {
  version: string;
  heading: string;
  lines: string[];
  button: string;
}

/** `POST /api/cardholder/cards/{id}/quotes` 201 body. */
export interface Quote {
  reference: string;
  amount: string;
  fee: string;
  total: string;
  from: string;
  to: string;
  wording: Wording;
  createdAt: string;
  origin: PaymentOrigin;
}

/** `GET /api/fake-bank` / `PUT /api/fake-bank/steering` body. */
export interface FakeBank {
  nextDebitOutcome: NextDebitOutcome;
  nextStatusCheck: NextStatusCheck;
  account: { lastFour: string; availableBalance: string };
  entries: AccountEntry[];
}

/** `PUT /api/fake-bank/steering` body: absent fields keep their value. */
export interface SteeringInput {
  nextDebitOutcome?: NextDebitOutcome;
  nextStatusCheck?: NextStatusCheck;
}

/** `GET /api/cards/{id}/payments` item (every reference, newest first). */
export interface PaymentListItem {
  reference: string;
  status: QuoteStatus;
  state: PaymentState | null;
  amount: string;
  createdAt: string;
  settledAt: string | null;
  failureReason: FailureReason | null;
  origin: PaymentOrigin;
}

/** `GET /api/cards/{id}/payments/{reference}` (AC-20). */
export interface PaymentRecord {
  quote: {
    reference: string;
    kind: 'full';
    amount: string;
    fee: string;
    total: string;
    from: string;
    to: string;
    wording: Wording;
    createdAt: string;
  };
  status: QuoteStatus;
  refusalReason: string | null;
  balanceAtRefusal: string | null;
  acceptance: {
    at: string | null;
    actor: { kind: 'cardholder' | 'admin'; name: string };
    authorityBasis: string;
    origin: PaymentOrigin;
  };
  payment: {
    state: PaymentState;
    amount: string;
    fee: string;
    createdAt: string;
    settledAt: string | null;
    effectiveAt: string | null;
    bankDebitedAt: string | null;
    failureReason: FailureReason | null;
    balanceBefore: string | null;
    balanceAfter: string | null;
    simulated: boolean;
  } | null;
  events: {
    at: string;
    kind: string;
    from: PaymentState | null;
    to: PaymentState | null;
    detail: string | null;
    simulated: boolean;
  }[];
}
