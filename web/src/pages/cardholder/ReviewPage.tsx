import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useApi } from '../../api/ApiContext';
import { ApiError } from '../../api/apiClient';
import type { ProblemDetails, Quote } from '../../api/types';
import { Button } from '../../components/Button';
import { Notice } from '../../components/Notice';
import styles from './Cardholder.module.css';

type State =
  | { kind: 'loading' }
  | { kind: 'quote'; quote: Quote }
  | { kind: 'refused'; problem: ProblemDetails | null; quote: Quote | null }
  | { kind: 'error' };

/** Splits a wording line at its first colon for the definition list: "Amount: £150.50" → ["Amount:", "£150.50"]. */
function splitLine(line: string): [string, string] {
  const at = line.indexOf(': ');
  return at < 0 ? ['', line] : [line.slice(0, at + 1), line.slice(at + 2)];
}

/**
 * Route `/cardholder/:id/pay`: the one review screen (002 R-4, AC-6, OB-7/OB-8; §4.1). On mount calls `createQuote(id)`
 * once (guarded against the StrictMode double effect; PLANNER-DECISION: each visit mints a new reference). Renders, in the
 * frame's single `<main>` and with nothing else: heading = `wording.heading`; a `<dl>` with `wording.lines` in order (each
 * line split at its first colon into `<dt>` and `<dd>`, text unchanged); the primary button labelled `wording.button`
 * (while confirming: pending state with `aria-disabled="true"` and a click guard, focus stays, 001 BUG-010); a "Back"
 * link to `/cardholder/:id`. Confirm → `confirmPayment(id, reference)` → navigate to
 * `/cardholder/:id/payments/:reference` with the receipt in navigation state. 409 on quote → the problem `message` in
 * a Notice (role="alert") with "Back"; 409 balance_changed / quote_refused on confirm → the `message` (W-6) in a
 * Notice (role="alert") plus a "Review again" button that mints a new quote; payment_pending → W-9 Notice with a link
 * "Back to your card". Never renders the fake-bank control.
 */
export function ReviewPage() {
  const api = useApi();
  const navigate = useNavigate();
  const { id = '' } = useParams();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [confirming, setConfirming] = useState(false);
  const alive = useRef(false);
  const started = useRef(false);
  const inFlight = useRef(false);

  const mint = useCallback(() => {
    setState({ kind: 'loading' });
    api
      .createQuote(id)
      .then((quote) => {
        if (alive.current) {
          setState({ kind: 'quote', quote });
        }
      })
      .catch((error: unknown) => {
        if (alive.current) {
          setState(
            error instanceof ApiError && error.status === 409
              ? { kind: 'refused', problem: error.problem, quote: null }
              : { kind: 'error' },
          );
        }
      });
  }, [api, id]);

  useEffect(() => {
    alive.current = true;
    if (!started.current) {
      started.current = true;
      mint();
    }
    return () => {
      alive.current = false;
    };
  }, [mint]);

  function confirm(quote: Quote): void {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setConfirming(true);
    api
      .confirmPayment(id, quote.reference)
      .then((receipt) => {
        if (alive.current) {
          void navigate(
            `/cardholder/${encodeURIComponent(id)}/payments/${encodeURIComponent(quote.reference)}`,
            { state: receipt },
          );
        }
      })
      .catch((error: unknown) => {
        if (alive.current) {
          setState(
            error instanceof ApiError && error.status === 409
              ? { kind: 'refused', problem: error.problem, quote }
              : { kind: 'error' },
          );
        }
      })
      .finally(() => {
        inFlight.current = false;
        if (alive.current) {
          setConfirming(false);
        }
      });
  }

  const cardPath = `/cardholder/${encodeURIComponent(id)}`;

  if (state.kind === 'loading') {
    return <p>Preparing your payment…</p>;
  }
  if (state.kind === 'error') {
    return (
      <div className={styles['page']}>
        <Notice tone="error" title="Something went wrong">
          This page could not be completed. Go back to your card to see its current state.
        </Notice>
        <Link to={cardPath}>Back</Link>
      </div>
    );
  }
  if (state.kind === 'refused') {
    const code = state.problem?.code;
    const reviewable =
      state.quote !== null && (code === 'balance_changed' || code === 'quote_refused');
    return (
      <div className={styles['page']}>
        <Notice tone="error">{state.problem?.message ?? 'The payment was refused.'}</Notice>
        <div className={styles['actions']}>
          {reviewable && (
            <Button variant="primary" onClick={mint}>
              Review again
            </Button>
          )}
          <Link to={cardPath}>{code === 'payment_pending' ? 'Back to your card' : 'Back'}</Link>
        </div>
      </div>
    );
  }

  const { quote } = state;
  return (
    <div className={styles['page']}>
      <h1 className={styles['title']}>{quote.wording.heading}</h1>
      <dl className={styles['lines']}>
        {quote.wording.lines.map((line) => {
          const [label, value] = splitLine(line);
          return (
            <div key={line} className={styles['line']}>
              <dt>{label}</dt> <dd>{value}</dd>
            </div>
          );
        })}
      </dl>
      <div className={styles['actions']}>
        <Button
          pending={confirming}
          pendingLabel="Confirming…"
          onClick={() => {
            confirm(quote);
          }}
        >
          {quote.wording.button}
        </Button>
        <Link to={cardPath}>Back</Link>
      </div>
    </div>
  );
}
