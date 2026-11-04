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
