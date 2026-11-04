import type { ReactNode } from 'react';
import { notImplemented } from '../notImplemented';

/**
 * Inline notice (no toasts, no modals): tone success/info → role="status", error → role="alert"; optional title.
 * Implemented in PR-02 (with Notice.module.css).
 */
export function Notice(props: {
  tone: 'success' | 'error' | 'info';
  title?: string;
  children: ReactNode;
}) {
  return notImplemented('PR-02', props);
}
