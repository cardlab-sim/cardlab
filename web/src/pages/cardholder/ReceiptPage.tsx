import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';
import { useApi } from '../../api/ApiContext';
import { ApiError } from '../../api/apiClient';
import type { PaymentState, Receipt } from '../../api/types';
import { Badge } from '../../components/Badge';
import { Button } from '../../components/Button';
import { Notice } from '../../components/Notice';
import { failureReasonLabel, paymentStateLabel } from '../../format/labels';
import { formatMoney } from '../../format/money';
import { formatLondonTime } from '../../format/time';
import styles from './Cardholder.module.css';

type State =
  | { kind: 'loading' }
  | { kind: 'notFound' }
  | { kind: 'error' }
  | { kind: 'loaded'; receipt: Receipt };

const HEADINGS: Record<PaymentState, string> = {
  completed: 'Payment complete',
  failed: 'Payment not made',
  pending: 'Payment pending',
};

const TONES = { completed: 'success', failed: 'error', pending: 'info' } as const;

function isReceipt(value: unknown, reference: string): value is Receipt {
  return (
    typeof value === 'object' &&
    value !== null &&
    (value as { reference?: unknown }).reference === reference
  );
}

/**
 * Route `/cardholder/:id/payments/:reference` (002 R-12, §4.1). Uses the receipt from navigation state, else
 * `getReceipt(id, reference)`. Heading by state: "Payment complete" / "Payment not made" / "Payment pending"; the
 * receipt `message` verbatim in a Notice (role="status" for pending/completed, role="alert" for failed); a `<dl>`
 * with Reference, Amount, Fee, Time (Europe/London), From, To, Outcome (text badge; plus a "Not received" reason badge
 * for not_received); a "Check status" button when pending (calls `checkPayment`, shows the new message, W-10 when
 * unanswered); for a failed payment a "Review again" link to `/cardholder/:id/pay`; link "Back to your card". Unknown
 * reference → Notice "This payment reference was not found". Never renders the fake-bank control.
 */
export function ReceiptPage() {
  const api = useApi();
  const { id = '', reference = '' } = useParams();
  const navState: unknown = useLocation().state;
  const [state, setState] = useState<State>(
    isReceipt(navState, reference) ? { kind: 'loaded', receipt: navState } : { kind: 'loading' },
  );
  const [checking, setChecking] = useState(false);
  const alive = useRef(false);

  useEffect(() => {
    alive.current = true;
    if (isReceipt(navState, reference)) {
      return () => {
        alive.current = false;
      };
    }
    api
      .getReceipt(id, reference)
      .then((receipt) => {
        if (alive.current) {
          setState({ kind: 'loaded', receipt });
        }
      })
      .catch((error: unknown) => {
        if (alive.current) {
          setState({
            kind: error instanceof ApiError && error.status === 404 ? 'notFound' : 'error',
          });
        }
      });
    return () => {
      alive.current = false;
    };
  }, [api, id, reference, navState]);

  function checkStatus(): void {
    setChecking(true);
    api
      .checkPayment(id, reference)
      .then((receipt) => {
        if (alive.current) {
          setState({ kind: 'loaded', receipt });
        }
      })
      .catch(() => {
        if (alive.current) {
          setState({ kind: 'error' });
        }
      })
      .finally(() => {
        if (alive.current) {
          setChecking(false);
        }
      });
  }

  const cardPath = `/cardholder/${encodeURIComponent(id)}`;

  if (state.kind === 'loading') {
    return <p>Loading…</p>;
  }
  if (state.kind === 'notFound' || state.kind === 'error') {
    return (
      <div className={styles['page']}>
        <Notice tone="error">
          {state.kind === 'notFound'
            ? 'This payment reference was not found'
            : 'This payment could not be loaded. Please try again.'}
        </Notice>
        <Link to={cardPath}>Back to your card</Link>
      </div>
    );
  }

  const { receipt } = state;
  return (
    <div className={styles['page']}>
      <h1 className={styles['title']}>{HEADINGS[receipt.state]}</h1>
      <Notice tone={TONES[receipt.state]}>{receipt.message}</Notice>
      <dl className={styles['details']}>
        <dt>Reference</dt>
        <dd>{receipt.reference}</dd>
        <dt>Amount</dt>
        <dd>{formatMoney(receipt.amount)}</dd>
        <dt>Fee</dt>
        <dd>{formatMoney(receipt.fee)}</dd>
        <dt>Time</dt>
        <dd>{formatLondonTime(receipt.time)}</dd>
        <dt>From</dt>
        <dd>{receipt.from}</dd>
        <dt>To</dt>
        <dd>{receipt.to}</dd>
        <dt>Outcome</dt>
        <dd>
          <Badge tone={receipt.state}>{paymentStateLabel(receipt.state)}</Badge>
          {receipt.failureReason === 'not_received' && (
            <Badge tone="failed">{failureReasonLabel(receipt.failureReason)}</Badge>
          )}
        </dd>
      </dl>
      <div className={styles['actions']}>
        {receipt.state === 'pending' && (
          <Button
            variant="secondary"
            pending={checking}
            pendingLabel="Checking…"
            onClick={checkStatus}
          >
            Check status
          </Button>
        )}
        {receipt.state === 'failed' && <Link to={`${cardPath}/pay`}>Review again</Link>}
        <Link to={cardPath}>Back to your card</Link>
      </div>
    </div>
  );
}
