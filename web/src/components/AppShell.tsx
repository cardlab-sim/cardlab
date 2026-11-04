import type { ReactNode } from 'react';
import { notImplemented } from '../notImplemented';

/**
 * Page frame (§4.1): `<header>` with "CardLab" (link to /), "Brightline Supplies Ltd" and "Sam Ellis"; `<main>` with
 * max-width 1200 px. A skip link "Skip to content" is the first focusable element.
 * Implemented in PR-02 (with AppShell.module.css).
 */
export function AppShell(props: { children: ReactNode }) {
  return notImplemented('PR-02', props);
}
