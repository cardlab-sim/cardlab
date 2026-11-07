import type { ReactNode } from 'react';
import styles from './Notice.module.css';

/** Inline notice (no toasts, no modals): tone success/info → role="status", error → role="alert"; optional title. */
export function Notice({
  tone,
  title,
  children,
}: {
  tone: 'success' | 'error' | 'info';
  title?: string;
  children: ReactNode;
}) {
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={`${styles['notice']} ${styles[tone]}`}
    >
      {title ? <strong className={styles['title']}>{title}</strong> : null}
      <div>{children}</div>
    </div>
  );
}
