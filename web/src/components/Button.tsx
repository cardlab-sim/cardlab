import type { ButtonHTMLAttributes } from 'react';
import { notImplemented } from '../notImplemented';

/** Props of Button: a native `<button>` plus a visual variant and a pending state. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** primary (default) | secondary | destructive. */
  variant?: 'primary' | 'secondary' | 'destructive';
  /** While true: disabled, `aria-busy="true"`, label replaced by `pendingLabel` (Submit Feedback rule). */
  pending?: boolean;
  /** Text shown while pending, e.g. "Saving…". Default "Working…". */
  pendingLabel?: string;
}

/**
 * Native button primitive (§4.1): ≥ 44 px tall, visible 2 px focus ring, tokens only, `type="button"` unless set.
 * Implemented in PR-02 (with Button.module.css).
 */
export function Button(props: ButtonProps) {
  return notImplemented('PR-02', props);
}
