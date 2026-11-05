import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useApi } from '../api/ApiContext';
import { ApiError } from '../api/apiClient';
import type { AuditEntry, CardDetail, Transaction } from '../api/types';
import { Badge } from '../components/Badge';
import { Notice } from '../components/Notice';
import { statusLabel } from '../format/labels';
import styles from './CardDetailPage.module.css';
import {
  AuditSection,
  LimitsSection,
  SimulateSection,
  StatusSection,
  TransactionsSection,
} from './cardDetail/sections';

type State =
  | { kind: 'loading' }
  | { kind: 'not-found' }
  | { kind: 'error' }
  | { kind: 'loaded'; card: CardDetail; transactions: Transaction[]; audit: AuditEntry[] };

/**
 * Route `/cards/:id` (§4.1): loads card, transactions and audit; heading with name, "•••• 0001" and status badge +
 * StatusSection; two columns at ≥ 1024 px (left LimitsSection + AuditSection, right SimulateSection +
 * TransactionsSection), stacked below. After any successful action, refetches card, transactions and audit
 * (the old data stays on screen until the new data arrives; a failed refetch keeps it and adds an error Notice;
 * only the newest fetch is applied).
 * Unknown card (404) → Notice "Card not found" with a link back to the list; other load errors → error Notice.
 */
export function CardDetailPage() {
  const api = useApi();
  const { id = '' } = useParams();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [refreshFailed, setRefreshFailed] = useState(false);
  // Sequence number of the latest fetch: an older response (or one after unmount) is ignored.
  const seq = useRef(0);

  const fetchAll = useCallback(async (): Promise<State> => {
    try {
      const [card, transactions, audit] = await Promise.all([
        api.getCard(id),
        api.listTransactions(id),
        api.listAudit(id),
      ]);
      return { kind: 'loaded', card, transactions, audit };
    } catch (error) {
      return error instanceof ApiError && error.status === 404
        ? { kind: 'not-found' }
        : { kind: 'error' };
    }
  }, [api, id]);

  useEffect(() => {
    const latest = seq;
    const mine = ++latest.current;
    void fetchAll().then((next) => {
      if (seq.current === mine) {
        setState(next);
      }
    });
    return () => {
      latest.current++;
    };
  }, [fetchAll]);

  // Resolves when the refetch has been applied (or superseded), so action buttons can stay pending until then.
  const refresh = async (): Promise<void> => {
    const mine = ++seq.current;
    const next = await fetchAll();
    if (seq.current !== mine) {
      return;
    }
    if (next.kind === 'error') {
      setRefreshFailed(true);
      return;
    }
    setRefreshFailed(false);
    setState(next);
  };

  if (state.kind === 'loading') {
    return <p>Loading card…</p>;
  }
  if (state.kind === 'not-found') {
    return (
      <Notice tone="error" title="Card not found">
        No card exists with this id. <Link to="/">Back to the card list</Link>
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

  const { card } = state;
  return (
    <div>
      <header className={styles['header']}>
        <h1 className={styles['title']}>{card.name}</h1>
        <span className={styles['lastFour']}>•••• {card.lastFour}</span>
        <Badge tone={card.status}>{statusLabel(card.status)}</Badge>
        <StatusSection card={card} onChanged={refresh} />
      </header>
      {refreshFailed && (
        <Notice tone="error" title="Could not refresh the card">
          Showing the last loaded data. Refresh the page to try again.
        </Notice>
      )}
      <div className={styles['columns']}>
        <div className={styles['column']}>
          <LimitsSection cardId={card.id} limits={card.limits} onChanged={refresh} />
          <AuditSection entries={state.audit} />
        </div>
        <div className={styles['column']}>
          <SimulateSection cardId={card.id} onDecided={refresh} />
          <TransactionsSection transactions={state.transactions} />
        </div>
      </div>
    </div>
  );
}
