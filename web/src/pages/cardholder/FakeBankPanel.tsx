import { useEffect, useId, useState } from 'react';
import { useApi } from '../../api/ApiContext';
import type { FakeBank, NextDebitOutcome, NextStatusCheck } from '../../api/types';
import { Button } from '../../components/Button';
import { SelectField } from '../../components/Field';
import { NEXT_DEBIT_OUTCOMES, NEXT_STATUS_CHECKS } from '../../format/labels';
import styles from './Cardholder.module.css';

/**
 * The labelled fake-bank control (002 R-15, DEC-6, DEC-7, CQ-8): `<section aria-labelledby>` with heading
 * "Fake bank (simulation control)", visually separated, rendered last on the cardholder view only. Two labelled selects
 * ("Next debit outcome": `NEXT_DEBIT_OUTCOMES`; "Next status check": `NEXT_STATUS_CHECKS`) and a "Set" button calling
 * `setFakeBank` with both values; text showing the current setting from `getFakeBank()`. Changes nothing else.
 * @param props.onChanged - Called after a successful Set (the view may re-read).
 */
export function FakeBankPanel({ onChanged }: { onChanged?: () => void }) {
  const api = useApi();
  const headingId = useId();
  const [bank, setBank] = useState<FakeBank | null>(null);
  const [outcome, setOutcome] = useState<NextDebitOutcome>('complete');
  const [check, setCheck] = useState<NextStatusCheck>('answers');
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .getFakeBank()
      .then((loaded) => {
        if (!cancelled) {
          setBank(loaded);
          setOutcome(loaded.nextDebitOutcome);
          setCheck(loaded.nextStatusCheck);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [api]);

  const labelOf = (options: readonly { value: string; label: string }[], value: string): string =>
    options.find((o) => o.value === value)?.label ?? value;

  function set(): void {
    setSaving(true);
    api
      .setFakeBank({
        nextDebitOutcome: outcome,
        nextStatusCheck: check,
      })
      .then((updated) => {
        setBank(updated);
        onChanged?.();
      })
      .catch(() => {
        setFailed(true);
      })
      .finally(() => {
        setSaving(false);
      });
  }

  return (
    <section aria-labelledby={headingId} className={styles['fakeBank']}>
      <h2 id={headingId}>Fake bank (simulation control)</h2>
      {failed && <p role="alert">The fake bank could not be reached.</p>}
      {bank !== null && (
        <>
          <p>
            Current setting: next debit outcome{' '}
            {labelOf(NEXT_DEBIT_OUTCOMES, bank.nextDebitOutcome)}; next status check{' '}
            {labelOf(NEXT_STATUS_CHECKS, bank.nextStatusCheck)}.
          </p>
          <SelectField
            id={`${headingId}-outcome`}
            label="Next debit outcome"
            value={outcome}
            options={NEXT_DEBIT_OUTCOMES}
            onChange={(value) => {
              setOutcome(
                (prev) => NEXT_DEBIT_OUTCOMES.find((o) => o.value === value)?.value ?? prev,
              );
            }}
          />
          <SelectField
            id={`${headingId}-check`}
            label="Next status check"
            value={check}
            options={NEXT_STATUS_CHECKS}
            onChange={(value) => {
              setCheck((prev) => NEXT_STATUS_CHECKS.find((o) => o.value === value)?.value ?? prev);
            }}
          />
          <div>
            <Button variant="secondary" pending={saving} pendingLabel="Setting…" onClick={set}>
              Set
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
