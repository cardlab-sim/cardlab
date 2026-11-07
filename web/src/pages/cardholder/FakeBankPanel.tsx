import { notImplemented } from '../../notImplemented';

/**
 * The labelled fake-bank control (002 R-15, DEC-6, DEC-7, CQ-8): `<section aria-labelledby>` with heading
 * "Fake bank (simulation control)", visually separated, rendered last on the cardholder view only. Two labelled selects
 * ("Next debit outcome": `NEXT_DEBIT_OUTCOMES`; "Next status check": `NEXT_STATUS_CHECKS`) and a "Set" button calling
 * `setFakeBank`; text showing the current setting from `getFakeBank()`. Changes nothing else. Implemented in PR-03.
 * @param props.onChanged - Called after a successful Set (the view may re-read).
 */
export function FakeBankPanel(props: { onChanged?: () => void }): never {
  return notImplemented('PR-03', props);
}
