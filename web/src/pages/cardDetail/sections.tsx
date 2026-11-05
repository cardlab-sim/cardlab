import { useState } from 'react';
import { useApi } from '../../api/ApiContext';
import { ApiError } from '../../api/apiClient';
import type {
  AuditEntry,
  CardDetail,
  Category,
  Decision,
  Limit,
  Period,
  Problem,
  Transaction,
} from '../../api/types';
import { CATEGORIES, PERIODS } from '../../api/types';
import { Badge } from '../../components/Badge';
import { Button } from '../../components/Button';
import { DataTable, type Column } from '../../components/DataTable';
import { TextField, SelectField } from '../../components/Field';
import { Notice } from '../../components/Notice';
import {
  auditActionLabel,
  categoryLabel,
  outcomeLabel,
  periodLabel,
  reasonLabel,
} from '../../format/labels';
import { formatMoney } from '../../format/money';
import { formatLondonTime } from '../../format/time';
import styles from '../CardDetailPage.module.css';

/** Freeze/Unfreeze button (label by status) with pending state and an inline Notice on error. */
export function StatusSection({ card, onChanged }: { card: CardDetail; onChanged: () => void }) {
  const api = useApi();
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);
  const frozen = card.status === 'frozen';

  const toggle = async (): Promise<void> => {
    setFailed(false);
    setPending(true);
    try {
      await (frozen ? api.unfreeze(card.id) : api.freeze(card.id));
      onChanged();
    } catch {
      setFailed(true);
    } finally {
      setPending(false);
    }
  };

  return (
    <div>
      <Button
        variant={frozen ? 'secondary' : 'destructive'}
        pending={pending}
        pendingLabel={frozen ? 'Unfreezing…' : 'Freezing…'}
        onClick={() => {
          void toggle();
        }}
      >
        {frozen ? 'Unfreeze' : 'Freeze'}
      </Button>
      {failed && (
        <Notice tone="error" title="Could not change the card status">
          Something went wrong. Please try again.
        </Notice>
      )}
    </div>
  );
}

const NUMERIC_AMOUNT = /^\d+(\.\d+)?$/;
const GENERIC_AMOUNT_ERROR = 'The limit could not be saved. Please try again.';

function amountErrorFrom(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem?.errors?.['amount']?.[0] ?? GENERIC_AMOUNT_ERROR;
  }
  return GENERIC_AMOUNT_ERROR;
}

/**
 * "Spend limits": DataTable (Category, Period, Amount £, Remove button per row named "Remove Fuel day limit") and an
 * inline form (category select, period select, amount input, Save). Saving an existing (category, period) replaces it.
 * Invalid amount → inline field error from the 400 ProblemDetails (or client-side check for empty/non-numeric).
 */
export function LimitsSection({
  cardId,
  limits,
  onChanged,
}: {
  cardId: string;
  limits: Limit[];
  onChanged: () => void;
}) {
  const api = useApi();
  const [category, setCategory] = useState<Category>('travel');
  const [period, setPeriod] = useState<Period>('day');
  const [amount, setAmount] = useState('');
  const [amountError, setAmountError] = useState<string | undefined>();
  const [saving, setSaving] = useState(false);
  const [removeFailed, setRemoveFailed] = useState(false);

  const save = async (): Promise<void> => {
    const trimmed = amount.trim();
    if (!NUMERIC_AMOUNT.test(trimmed)) {
      setAmountError('Enter the limit as a number, for example 50.00.');
      return;
    }
    setAmountError(undefined);
    setSaving(true);
    try {
      await api.setLimit(cardId, category, period, trimmed);
      setAmount('');
      onChanged();
    } catch (error) {
      setAmountError(amountErrorFrom(error));
    } finally {
      setSaving(false);
    }
  };

  const columns: readonly Column<Limit>[] = [
    { header: 'Category', cell: (limit) => categoryLabel(limit.category) },
    { header: 'Period', cell: (limit) => periodLabel(limit.period) },
    { header: 'Amount', numeric: true, cell: (limit) => formatMoney(limit.amount) },
    {
      header: 'Remove',
      cell: (limit) => (
        <Button
          variant="secondary"
          onClick={() => {
            setRemoveFailed(false);
            api.removeLimit(cardId, limit.category, limit.period).then(onChanged, () => {
              setRemoveFailed(true);
            });
          }}
        >
          Remove {`${categoryLabel(limit.category)} ${periodLabel(limit.period).toLowerCase()}`}{' '}
          limit
        </Button>
      ),
    },
  ];

  return (
    <section aria-labelledby="limits-heading" className={styles['section']}>
      <h2 id="limits-heading">Spend limits</h2>
      {removeFailed && (
        <Notice tone="error" title="Could not remove the limit">
          Something went wrong. Please try again.
        </Notice>
      )}
      <DataTable
        caption="Limits"
        columns={columns}
        rows={limits}
        rowKey={(limit) => `${limit.category}/${limit.period}`}
        emptyText="No spend limits are set."
      />
      <form
        className={styles['form']}
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <SelectField
          id="limit-category"
          label="Category"
          value={category}
          options={CATEGORIES}
          onChange={(value) => {
            setCategory(value as Category);
          }}
        />
        <SelectField
          id="limit-period"
          label="Period"
          value={period}
          options={PERIODS}
          onChange={(value) => {
            setPeriod(value as Period);
          }}
        />
        <TextField
          id="limit-amount"
          label="Amount"
          inputMode="decimal"
          value={amount}
          error={amountError}
          onChange={(event) => {
            setAmount(event.target.value);
          }}
        />
        <div className={styles['actions']}>
          <Button type="submit" pending={saving} pendingLabel="Saving…">
            Save
          </Button>
        </div>
      </form>
    </section>
  );
}

function auditValue(entry: AuditEntry, value: string | null): string {
  if (value === null) {
    return '—';
  }
  return entry.action.startsWith('limit_') ? formatMoney(value) : value;
}

/** "Audit": ordered list newest first: action label, actor, London time, old → new value (limit amounts as money). */
export function AuditSection({ entries }: { entries: AuditEntry[] }) {
  return (
    <section aria-labelledby="audit-heading" className={styles['section']}>
      <h2 id="audit-heading">Audit</h2>
      {entries.length === 0 ? (
        <p>No changes yet.</p>
      ) : (
        <ol className={styles['auditList']}>
          {entries.map((entry) => (
            <li key={entry.id} className={styles['auditItem']}>
              <strong>{auditActionLabel(entry.action)}</strong>
              <span className={styles['auditMeta']}>
                {entry.actorName} · {formatLondonTime(entry.atUtc)}
              </span>
              <span>
                {auditValue(entry, entry.oldValue)} → {auditValue(entry, entry.newValue)}
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

type SimulateResult =
  | { kind: 'decision'; decision: Decision }
  | { kind: 'problems'; problems: Problem[] }
  | { kind: 'failed' };

const FIELD_PROBLEMS = ['amount', 'category', 'merchant'];

/**
 * "Simulate purchase": amount, category select, merchant (all labelled), Submit with pending state; result Notice shows
 * outcome badge, the reason code (e.g. `limit_exceeded`) and remaining per limit; `problems` shown inline per field
 * (a problem for any other field is listed in an error Notice). `onDecided` is called for every decision except
 * invalid_request, so the caller can refetch.
 */
export function SimulateSection({
  cardId,
  onDecided,
}: {
  cardId: string;
  onDecided: (decision: Decision) => void;
}) {
  const api = useApi();
  const [amount, setAmount] = useState('');
  const [category, setCategory] = useState<Category>('travel');
  const [merchant, setMerchant] = useState('');
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<SimulateResult | null>(null);

  const submit = async (): Promise<void> => {
    setResult(null);
    setPending(true);
    try {
      const decision = await api.simulatePurchase(cardId, {
        amount: amount.trim(),
        category,
        merchant,
      });
      if (decision.problems.length > 0) {
        setResult({ kind: 'problems', problems: decision.problems });
      } else {
        setResult({ kind: 'decision', decision });
        onDecided(decision);
      }
    } catch {
      setResult({ kind: 'failed' });
    } finally {
      setPending(false);
    }
  };

  const problems = result?.kind === 'problems' ? result.problems : [];
  const fieldError = (field: string): string | undefined =>
    problems.find((problem) => problem.field === field)?.message;
  const otherProblems = problems.filter((problem) => !FIELD_PROBLEMS.includes(problem.field));

  return (
    <section aria-labelledby="simulate-heading" className={styles['section']}>
      <h2 id="simulate-heading">Simulate purchase</h2>
      <form
        className={styles['form']}
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        <TextField
          id="simulate-amount"
          label="Amount"
          inputMode="decimal"
          value={amount}
          error={fieldError('amount')}
          onChange={(event) => {
            setAmount(event.target.value);
          }}
        />
        <SelectField
          id="simulate-category"
          label="Category"
          value={category}
          options={CATEGORIES}
          error={fieldError('category')}
          onChange={(value) => {
            setCategory(value as Category);
          }}
        />
        <TextField
          id="simulate-merchant"
          label="Merchant"
          value={merchant}
          error={fieldError('merchant')}
          onChange={(event) => {
            setMerchant(event.target.value);
          }}
        />
        <div className={styles['actions']}>
          <Button type="submit" pending={pending} pendingLabel="Simulating…">
            Submit
          </Button>
        </div>
      </form>
      {otherProblems.length > 0 && (
        <Notice tone="error" title="The purchase was not accepted">
          {otherProblems.map((problem) => problem.message).join(' ')}
        </Notice>
      )}
      {result?.kind === 'failed' && (
        <Notice tone="error" title="Could not simulate the purchase">
          Something went wrong. Please try again.
        </Notice>
      )}
      {result?.kind === 'decision' && <DecisionNotice decision={result.decision} />}
    </section>
  );
}

function DecisionNotice({ decision }: { decision: Decision }) {
  return (
    <Notice tone={decision.outcome === 'approved' ? 'success' : 'info'} title="Result">
      <Badge tone={decision.outcome}>{outcomeLabel(decision.outcome)}</Badge>{' '}
      <span>
        Reason: <code>{decision.reason}</code>
      </span>
      {decision.limits.length > 0 && (
        <ul className={styles['remaining']}>
          {decision.limits.map((limit) => (
            <li key={`${limit.category}/${limit.period}`}>
              {categoryLabel(limit.category)} {periodLabel(limit.period).toLowerCase()}:{' '}
              {formatMoney(limit.remaining)} remaining of {formatMoney(limit.limit)}
            </li>
          ))}
        </ul>
      )}
    </Notice>
  );
}

const TRANSACTION_COLUMNS: readonly Column<Transaction>[] = [
  { header: 'Time', cell: (tx) => formatLondonTime(tx.occurredAt) },
  { header: 'Merchant', cell: (tx) => tx.merchant },
  { header: 'Category', cell: (tx) => categoryLabel(tx.category) },
  { header: 'Amount', numeric: true, cell: (tx) => formatMoney(tx.amount) },
  {
    header: 'Outcome',
    cell: (tx) => <Badge tone={tx.outcome}>{outcomeLabel(tx.outcome)}</Badge>,
  },
  { header: 'Reason', cell: (tx) => reasonLabel(tx.reason) },
];

/** "Transactions": DataTable newest first: Time (London), Merchant, Category, Amount, Outcome badge, Reason. */
export function TransactionsSection({ transactions }: { transactions: Transaction[] }) {
  return (
    <section aria-labelledby="transactions-heading" className={styles['section']}>
      <h2 id="transactions-heading">Transactions</h2>
      <DataTable
        caption="Transaction history"
        columns={TRANSACTION_COLUMNS}
        rows={transactions}
        rowKey={(tx) => tx.id}
        emptyText="No transactions yet."
      />
    </section>
  );
}
