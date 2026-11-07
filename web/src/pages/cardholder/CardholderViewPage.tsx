import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { useApi } from '../../api/ApiContext';
import { ApiError } from '../../api/apiClient';
import type { AccountEntry, ActivityEntry, CardholderView } from '../../api/types';
import { Badge } from '../../components/Badge';
import { Button } from '../../components/Button';
import buttonStyles from '../../components/Button.module.css';
import { DataTable, type Column } from '../../components/DataTable';
import { Notice } from '../../components/Notice';
import { activityKindLabel, outcomeLabel, paymentStateLabel } from '../../format/labels';
import { formatMoney } from '../../format/money';
import { formatLondonTime } from '../../format/time';
import { FakeBankPanel } from './FakeBankPanel';
import styles from './Cardholder.module.css';

const CARD_COLUMNS: readonly Column<ActivityEntry>[] = [
  { header: 'Date', cell: (row) => formatLondonTime(row.at) },
  { header: 'Description', cell: (row) => row.description },
  { header: 'Amount', cell: (row) => formatMoney(row.amount), numeric: true },
  { header: 'Type', cell: (row) => <Badge tone="neutral">{activityKindLabel(row.kind)}</Badge> },
  {
    header: 'Status',
    cell: (row) =>
      row.state !== null ? (
        <Badge tone={row.state}>{paymentStateLabel(row.state)}</Badge>
      ) : row.outcome !== null ? (
        <Badge tone={row.outcome}>{outcomeLabel(row.outcome)}</Badge>
      ) : null,
  },
];

const ACCOUNT_COLUMNS: readonly Column<AccountEntry>[] = [
  { header: 'Date', cell: (row) => formatLondonTime(row.postedAt) },
  { header: 'Reference', cell: (row) => row.reference },
  { header: 'Amount', cell: (row) => formatMoney(row.amount), numeric: true },
];

type State =
  | { kind: 'loading' }
  | { kind: 'notFound' }
  | { kind: 'error' }
  | { kind: 'loaded'; view: CardholderView };

function Section({ title, children }: { title: string; children: ReactNode }) {
  const headingId = useId();
  return (
    <section aria-labelledby={headingId} className={styles['section']}>
      <h2 id={headingId}>{title}</h2>
      {children}
    </section>
  );
}

/**
 * Route `/cardholder/:id` (002 §4.1, DEC-11, DEC-13). Loads `getCardholderView(id)`; when `pendingPayment` is set it
 * calls `checkPayment(id, reference)` once per reference and then re-reads the view (auto status check). Sections in DOM
 * order: heading (card name, "•••• 0001"); **Balance** (32 px figure; the primary link "Pay full balance" to
 * `/cardholder/:id/pay` when availability is `available`, else `availabilityMessage` verbatim, and for `payment_pending`
 * a "Check status" button); **Pending payment** Notice (W-5 message, role="status", "Check status" button); **Linked
 * current account** (display name, "ending 7731", available balance, authority sentence); **Recent card activity** table
 * (date, description, amount, type badge, state/outcome badge); **Current account recent activity** table (date,
 * reference, amount); last, `<FakeBankPanel />`. Unknown card → Notice "Card not found".
 */
export function CardholderViewPage() {
  const api = useApi();
  const { id = '' } = useParams();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [checking, setChecking] = useState(false);
  const alive = useRef(false);
  const checked = useRef<string | null>(null);

  const fail = useCallback((error: unknown) => {
    if (alive.current) {
      setState({ kind: error instanceof ApiError && error.status === 404 ? 'notFound' : 'error' });
    }
  }, []);

  const load = useCallback(
    async (autoCheck: boolean) => {
      let view = await api.getCardholderView(id);
      const pending = view.pendingPayment;
      if (autoCheck && pending !== null && checked.current !== pending.reference) {
        checked.current = pending.reference;
        await api.checkPayment(id, pending.reference);
        view = await api.getCardholderView(id);
      }
      if (alive.current) {
        setState({ kind: 'loaded', view });
      }
    },
    [api, id],
  );

  useEffect(() => {
    alive.current = true;
    load(true).catch(fail);
    return () => {
      alive.current = false;
    };
  }, [load, fail]);

  function checkStatus(reference: string): void {
    setChecking(true);
    api
      .checkPayment(id, reference)
      .then(() => load(false))
      .catch(fail)
      .finally(() => {
        if (alive.current) {
          setChecking(false);
        }
      });
  }

  if (state.kind === 'loading') {
    return <p>Loading…</p>;
  }
  if (state.kind === 'notFound') {
    return (
      <Notice tone="error" title="Card not found">
        This card could not be found.
      </Notice>
    );
  }
  if (state.kind === 'error') {
    return (
      <Notice tone="error" title="Could not load the card">
        Something went wrong while loading the card. Please refresh the page to try again.
      </Notice>
    );
  }

  const { view } = state;
  const pending = view.pendingPayment;
  const checkButton = (reference: string) => (
    <Button
      variant="secondary"
      pending={checking}
      pendingLabel="Checking…"
      onClick={() => {
        checkStatus(reference);
      }}
    >
      Check status
    </Button>
  );

  return (
    <div className={styles['page']}>
      <h1 className={styles['title']}>
        {view.card.name} <span className={styles['lastFour']}>•••• {view.card.lastFour}</span>
      </h1>

      <Section title="Balance">
        <p className={styles['balance']}>{formatMoney(view.balanceOwed)}</p>
        {view.availability === 'available' ? (
          <div>
            <Link
              to={`/cardholder/${encodeURIComponent(id)}/pay`}
              className={`${buttonStyles['button']} ${buttonStyles['primary']}`}
            >
              Pay full balance
            </Link>
          </div>
        ) : (
          <p>{view.availabilityMessage}</p>
        )}
        {view.availability === 'payment_pending' && pending !== null && (
          <div>{checkButton(pending.reference)}</div>
        )}
      </Section>

      {pending !== null && (
        <Section title="Pending payment">
          <Notice tone="info">{pending.message}</Notice>
          <div>{checkButton(pending.reference)}</div>
        </Section>
      )}

      <Section title="Linked current account">
        <dl className={styles['details']}>
          <dt>Account</dt>
          <dd>{view.account.displayName}</dd>
          <dt>Identifier</dt>
          <dd>ending {view.account.lastFour}</dd>
          <dt>Available balance</dt>
          <dd>{formatMoney(view.account.availableBalance)}</dd>
        </dl>
        <p>{view.account.authorityBasis}</p>
      </Section>

      <DataTable
        caption="Recent card activity"
        columns={CARD_COLUMNS}
        rows={view.cardActivity}
        rowKey={(row) => `${row.kind}-${row.reference ?? row.at}-${row.amount}-${row.description}`}
        emptyText="There is no card activity yet."
      />

      <DataTable
        caption="Current account recent activity"
        columns={ACCOUNT_COLUMNS}
        rows={view.accountActivity}
        rowKey={(row) => row.reference}
        emptyText="There is no account activity yet."
      />

      <FakeBankPanel
        onChanged={() => {
          load(false).catch(fail);
        }}
      />
    </div>
  );
}
