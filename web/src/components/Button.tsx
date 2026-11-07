import type { ButtonHTMLAttributes } from 'react';
import styles from './Button.module.css';

/** Props of Button: a native `<button>` plus a visual variant and a pending state. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** primary (default) | secondary | destructive. */
  variant?: 'primary' | 'secondary' | 'destructive';
  /** While true: `aria-disabled` (keeps focus), clicks ignored, `aria-busy="true"`, label replaced by `pendingLabel` (Submit Feedback rule). */
  pending?: boolean;
  /** Text shown while pending, e.g. "Saving…". Default "Working…". */
  pendingLabel?: string;
}

/**
 * Native button primitive (§4.1): ≥ 44 px tall, visible 2 px focus ring, tokens only, `type="button"` unless set.
 * While pending it stays focusable (`aria-disabled`, not native `disabled`) and ignores clicks and form submits (BUG-010).
 */
export function Button({
  variant = 'primary',
  pending = false,
  pendingLabel = 'Working…',
  type = 'button',
  disabled,
  onClick,
  className,
  children,
  ...rest
}: ButtonProps) {
  const classes = [styles['button'], styles[variant], className].filter(Boolean).join(' ');
  return (
    <button
      {...rest}
      type={type}
      className={classes}
      disabled={disabled}
      aria-disabled={pending ? true : undefined}
      onClick={(event) => {
        if (pending) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
      aria-busy={pending ? true : undefined}
    >
      {pending ? pendingLabel : children}
    </button>
  );
}
