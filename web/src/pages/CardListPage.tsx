import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { useApi } from '../api/ApiContext';
import type { CardSummary } from '../api/types';
import { Badge } from '../components/Badge';
import { DataTable, type Column } from '../components/DataTable';
import { Notice } from '../components/Notice';
import { statusLabel } from '../format/labels';
import styles from './CardListPage.module.css';

const COLUMNS: readonly Column<CardSummary>[] = [
  { header: 'Name', cell: (card) => card.name },
  { header: 'Cardholder', cell: (card) => card.cardholderName },
  { header: 'Last four', cell: (card) => card.lastFour },
  {
    header: 'Status',
    cell: (card) => <Badge tone={card.status}>{statusLabel(card.status)}</Badge>,
  },
  {
    header: 'Action',
    cell: (card) => (
      <span className={styles['actions']}>
        <Link
          to={`/cards/${encodeURIComponent(card.id)}`}
          className={styles['open']}
          aria-label={`Open ${card.name}`}
        >
          Open
        </Link>
        <Link
          to={`/cardholder/${encodeURIComponent(card.id)}`}
          className={styles['open']}
          aria-label={`Cardholder view for ${card.name}`}
        >
          Cardholder view
        </Link>
      </span>
    ),
  },
];

type State = { kind: 'loading' } | { kind: 'error' } | { kind: 'loaded'; cards: CardSummary[] };

/**
 * Route `/` (§4.1): heading "Cards" and one DataTable (Name, Cardholder, Last four, Status badge, "Open" link to
 * /cards/:id with an accessible name "Open Ops van"). Loading text while fetching; error Notice on failure.
 */
export function CardListPage() {
  const api = useApi();
  const [state, setState] = useState<State>({ kind: 'loading' });

  useEffect(() => {
    let cancelled = false;
    api
      .listCards()
      .then((cards) => {
        if (!cancelled) {
          setState({ kind: 'loaded', cards });
        }
      })
      .catch(() => {
        if (!cancelled) {
          setState({ kind: 'error' });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  return (
    <section>
      <h1>Cards</h1>
      {state.kind === 'loading' && <p>Loading cards…</p>}
      {state.kind === 'error' && (
        <Notice tone="error" title="Could not load cards">
          Something went wrong while loading the cards. Please refresh the page to try again.
        </Notice>
      )}
      {state.kind === 'loaded' && (
        <DataTable
          caption="Card list"
          columns={COLUMNS}
          rows={state.cards}
          rowKey={(card) => card.id}
          emptyText="There are no cards."
        />
      )}
    </section>
  );
}
