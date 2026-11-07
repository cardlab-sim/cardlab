import { Link } from 'react-router';
import type { ActivityEntry, PaymentListItem } from '../../api/types';
import { Badge } from '../../components/Badge';
import { DataTable, type Column } from '../../components/DataTable';
import {
  activityKindLabel,
  categoryLabel,
  failureReasonLabel,
  originLabel,
  outcomeLabel,
  paymentStateLabel,
  quoteStatusLabel,
  reasonLabel,
} from '../../format/labels';
import { formatMoney } from '../../format/money';
import { formatLondonTime } from '../../format/time';
import styles from '../CardDetailPage.module.css';

function recordPath(cardId: string, reference: string): string {
  return `/cards/${cardId}/payments/${reference}`;
}

function activityColumns(cardId: string): readonly Column<ActivityEntry>[] {
  return [
    { header: 'Time', cell: (row) => formatLondonTime(row.at) },
    { header: 'Type', cell: (row) => <Badge tone="neutral">{activityKindLabel(row.kind)}</Badge> },
    {
      header: 'Description',
      cell: (row) =>
        row.reference !== null ? (
          <Link to={recordPath(cardId, row.reference)}>{row.description}</Link>
        ) : (
          row.description
        ),
    },
    {
      header: 'Category',
      cell: (row) => (row.category !== null ? categoryLabel(row.category) : '—'),
    },
    { header: 'Amount', numeric: true, cell: (row) => formatMoney(row.amount) },
    {
      header: 'Outcome',
      cell: (row) =>
        row.state !== null ? (
          <Badge tone={row.state}>{paymentStateLabel(row.state)}</Badge>
        ) : row.outcome !== null ? (
          <Badge tone={row.outcome}>{outcomeLabel(row.outcome)}</Badge>
        ) : null,
    },
    {
      header: 'Reason',
      cell: (row) =>
        row.reason !== null ? (
          reasonLabel(row.reason)
        ) : row.origin === 'deploy_smoke' ? (
          <Badge tone="neutral">{originLabel(row.origin)}</Badge>
        ) : null,
    },
  ];
}

/**
 * 002 "Activity" (replaces 001's Transactions table on the card detail, DEC-12): DataTable with caption
 * "Transaction history" (kept so 001 page objects and AC-22/AC-38 still read it), newest first. Columns: Time (London),
 * Type (badge Purchase/Payment), Description (merchant, or a link "Payment PAY-…" to `/cards/:id/payments/:reference`),
 * Category (purchases; "—" for payments), Amount, Outcome (badge: Approved/Declined, or Pending/Completed/Failed),
 * Reason (purchase reason label; "origin: smoke" badge for smoke payments).
 * @param props.cardId - Card id (for record links).
 * @param props.entries - Rows from `listActivity`.
 */
export function ActivitySection({ cardId, entries }: { cardId: string; entries: ActivityEntry[] }) {
  const keys = new Map(entries.map((row, index) => [row, String(index)]));
  return (
    <section aria-labelledby="activity-heading" className={styles['section']}>
      <h2 id="activity-heading">Activity</h2>
      <DataTable
        caption="Transaction history"
        columns={activityColumns(cardId)}
        rows={entries}
        rowKey={(row) => keys.get(row) ?? ''}
        emptyText="No activity yet."
      />
    </section>
  );
}

function paymentColumns(cardId: string): readonly Column<PaymentListItem>[] {
  return [
    {
      header: 'Reference',
      cell: (item) => <Link to={recordPath(cardId, item.reference)}>{item.reference}</Link>,
    },
    { header: 'Amount', numeric: true, cell: (item) => formatMoney(item.amount) },
    { header: 'Created', cell: (item) => formatLondonTime(item.createdAt) },
    {
      header: 'Status',
      cell: (item) => <Badge tone="neutral">{quoteStatusLabel(item.status)}</Badge>,
    },
    {
      header: 'State',
      cell: (item) =>
        item.state !== null ? (
          <Badge tone={item.state}>{paymentStateLabel(item.state)}</Badge>
        ) : (
          '—'
        ),
    },
    { header: 'Origin', cell: (item) => <Badge tone="neutral">{originLabel(item.origin)}</Badge> },
    {
      header: 'Failure reason',
      cell: (item) => (item.failureReason !== null ? failureReasonLabel(item.failureReason) : '—'),
    },
  ];
}

/**
 * 002 "Payments": every reference newest first (incl. open and refused), each with reference link, amount, created
 * time, status and state badges, origin badge (DEC-15) and failure reason.
 * @param props.cardId - Card id (for record links).
 * @param props.payments - Items from `listPaymentRecords`.
 */
export function PaymentsSection({
  cardId,
  payments,
}: {
  cardId: string;
  payments: PaymentListItem[];
}) {
  return (
    <section aria-labelledby="payments-heading" className={styles['section']}>
      <h2 id="payments-heading">Payments</h2>
      <DataTable
        caption="Payment references"
        columns={paymentColumns(cardId)}
        rows={payments}
        rowKey={(item) => item.reference}
        emptyText="No payments yet."
      />
    </section>
  );
}
