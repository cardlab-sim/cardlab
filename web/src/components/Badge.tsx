import type { ReactNode } from 'react';
import styles from './Badge.module.css';

/**
 * Badge tones map to the §4.1 status tokens (active, frozen, approved, declined) or neutral; 002 adds the payment states
 * (pending uses the new pending tokens, completed reuses approved, failed reuses declined).
 */
export type BadgeTone =
  'active' | 'frozen' | 'approved' | 'declined' | 'neutral' | 'pending' | 'completed' | 'failed';

/** Text status badge: the text carries the meaning, colour only reinforces it (§4.1 "never colour alone"). */
export function Badge({ tone, children }: { tone: BadgeTone; children: ReactNode }) {
  return <span className={`${styles['badge']} ${styles[tone]}`}>{children}</span>;
}
