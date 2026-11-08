import { useEffect, useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { useApi } from '../api/ApiContext';
import { ApiError } from '../api/apiClient';
import type { PaymentRecord } from '../api/types';
import { Badge } from '../components/Badge';
import { DataTable, type Column } from '../components/DataTable';
import { Notice } from '../components/Notice';
import {
  failureReasonLabel,
  originLabel,
  paymentStateLabel,
  quoteStatusLabel,
} from '../format/labels';
import { formatMoney } from '../format/money';
import { formatLondonTime } from '../format/time';
import styles from './PaymentRecordPage.module.css';

type State =
  | { kind: 'loading' }
  | { kind: 'not-found' }
  | { kind: 'error' }
  | { kind: 'loaded'; record: PaymentRecord };

type RecordEvent = PaymentRecord['events'][number];

/** A term/definition pair; renders nothing for a null value so absent facts are not shown as zero. */
function Fact({ term, children }: { term: string; children: ReactNode }) {
  if (children === null) {
    return null;
  }
  return (
    <>
      <dt>{term}</dt>
      <dd>{children}</dd>
    </>
  );
}

function time(iso: string | null): string | null {
  return iso === null ? null : formatLondonTime(iso);
}

function money(amount: string | null): string | null {
  return amount === null ? null : formatMoney(amount);
}

const EVENT_COLUMNS: readonly Column<RecordEvent>[] = [
  { header: 'Time', cell: (event) => formatLondonTime(event.at) },
  { header: 'Event', cell: (event) => <code>{event.kind}</code> },
  {
    header: 'From → to',
    cell: (event) =>
      `${event.from === null ? '—' : paymentStateLabel(event.from)} → ${event.to === null ? '—' : paymentStateLabel(event.to)}`,
  },
  { header: 'Detail', cell: (event) => event.detail ?? '—' },
  { header: 'Simulated', cell: (event) => (event.simulated ? 'Yes' : '—') },
];

/**
 * Route `/cards/:id/payments/:reference`: the admin's read-only payment record (002 R-14, AC-20, §4.1). Loads
 * `getPaymentRecord(id, reference)`. Sections: "What the cardholder saw" (wording heading, lines verbatim, button text,
 * "Wording version v1"); "Acceptance" (time, actor name and kind, authority basis, origin badge); "Payment" (state badge,
 * amount, fee, balances before/after, effective time, settled time, bank debit time, "Simulated outcome" text when
 * simulated, failure reason) or "Refusal" (reason, balance at refusal); "Events" table (time, kind, from → to, detail,
 * simulated). Facts the record does not have (no balances for a pending payment, no effective time) are omitted.
 * Times Europe/London; amounts £1,234.56. The stored wording is shown verbatim, never re-rendered. 404 → Notice
 * "Payment not found" with a link back to the card.
 */
export function PaymentRecordPage() {
  const api = useApi();
  const { id = '', reference = '' } = useParams();
  const [state, setState] = useState<State>({ kind: 'loading' });

  useEffect(() => {
    let current = true;
    api
      .getPaymentRecord(id, reference)
      .then((record) => {
        if (current) {
          setState({ kind: 'loaded', record });
        }
      })
      .catch((error: unknown) => {
        if (current) {
          setState({
            kind: error instanceof ApiError && error.status === 404 ? 'not-found' : 'error',
          });
        }
      });
    return () => {
      current = false;
    };
  }, [api, id, reference]);

  const back = <Link to={`/cards/${id}`}>Back to the card</Link>;
  if (state.kind === 'loading') {
    return <p>Loading payment…</p>;
  }
  if (state.kind === 'not-found') {
    return (
      <Notice tone="error" title="Payment not found">
        No payment record exists with this reference on this card. {back}
      </Notice>
    );
  }
  if (state.kind === 'error') {
    return (
      <Notice tone="error" title="Could not load the payment">
        Something went wrong while loading the payment record. Please refresh the page to try again.
      </Notice>
    );
  }

  const { record } = state;
  const { quote, acceptance, payment } = record;
  return (
    <div className={styles['page']}>
      <header className={styles['header']}>
        <h1 className={styles['title']}>Payment {quote.reference}</h1>
        <Badge tone="neutral">{quoteStatusLabel(record.status)}</Badge>
        {payment !== null && <Badge tone={payment.state}>{paymentStateLabel(payment.state)}</Badge>}
        {acceptance.origin === 'deploy_smoke' && (
          <Badge tone="neutral">{originLabel(acceptance.origin)}</Badge>
        )}
        {back}
      </header>

      <section aria-labelledby="shown-heading" className={styles['section']}>
        <h2 id="shown-heading">What the cardholder saw</h2>
        <dl className={styles['facts']}>
          <Fact term="Amount">{formatMoney(quote.amount)}</Fact>
          <Fact term="Fee">{formatMoney(quote.fee)}</Fact>
          <Fact term="Total">{formatMoney(quote.total)}</Fact>
          <Fact term="From">{quote.from}</Fact>
          <Fact term="To">{quote.to}</Fact>
          <Fact term="Shown at">{formatLondonTime(quote.createdAt)}</Fact>
        </dl>
        <p className={styles['wordingHeading']}>{quote.wording.heading}</p>
        <ul className={styles['lines']}>
          {quote.wording.lines.map((line, index) => (
            <li key={`${index}/${line}`}>{line}</li>
          ))}
        </ul>
        <p>
          Button: <span className={styles['button']}>{quote.wording.button}</span>
        </p>
        <p>Wording version {quote.wording.version}</p>
      </section>

      <section aria-labelledby="acceptance-heading" className={styles['section']}>
        <h2 id="acceptance-heading">Acceptance</h2>
        <dl className={styles['facts']}>
          <Fact term="Accepted at">{time(acceptance.at) ?? '—'}</Fact>
          <Fact term="Accepted by">
            {acceptance.actor.name} ({acceptance.actor.kind})
          </Fact>
          <Fact term="Authority basis">{acceptance.authorityBasis}</Fact>
          <Fact term="Origin">
            <Badge tone="neutral">{originLabel(acceptance.origin)}</Badge>
          </Fact>
        </dl>
      </section>

      {payment !== null ? (
        <section aria-labelledby="payment-heading" className={styles['section']}>
          <h2 id="payment-heading">Payment</h2>
          <dl className={styles['facts']}>
            <Fact term="State">
              <Badge tone={payment.state}>{paymentStateLabel(payment.state)}</Badge>
            </Fact>
            <Fact term="Amount">{formatMoney(payment.amount)}</Fact>
            <Fact term="Fee">{formatMoney(payment.fee)}</Fact>
            <Fact term="Balance owed before">{money(payment.balanceBefore)}</Fact>
            <Fact term="Balance owed after">{money(payment.balanceAfter)}</Fact>
            <Fact term="Effective time">{time(payment.effectiveAt)}</Fact>
            <Fact term="Settled at">{time(payment.settledAt)}</Fact>
            <Fact term="Bank debit time">{time(payment.bankDebitedAt)}</Fact>
            <Fact term="Failure reason">
              {payment.failureReason === null ? null : failureReasonLabel(payment.failureReason)}
            </Fact>
            <Fact term="Simulated outcome">{payment.simulated ? 'Yes' : null}</Fact>
          </dl>
        </section>
      ) : record.status === 'refused' ? (
        <section aria-labelledby="refusal-heading" className={styles['section']}>
          <h2 id="refusal-heading">Refusal</h2>
          <dl className={styles['facts']}>
            <Fact term="Reason">
              {record.refusalReason === null ? null : <code>{record.refusalReason}</code>}
            </Fact>
            <Fact term="Balance owed at refusal">{money(record.balanceAtRefusal)}</Fact>
          </dl>
        </section>
      ) : (
        <section aria-labelledby="no-payment-heading" className={styles['section']}>
          <h2 id="no-payment-heading">Payment</h2>
          <p>No payment was made for this reference.</p>
        </section>
      )}

      <section aria-labelledby="events-heading" className={styles['section']}>
        <h2 id="events-heading">Events</h2>
        <DataTable
          caption="Events"
          columns={EVENT_COLUMNS}
          rows={record.events}
          rowKey={(event) => `${event.at}/${event.kind}/${record.events.indexOf(event)}`}
          emptyText="No events yet."
        />
      </section>
    </div>
  );
}
