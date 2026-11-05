import type { ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './AppShell.module.css';

/**
 * Page frame (§4.1): `<header>` with "CardLab" (link to /), "Brightline Supplies Ltd" and "Sam Ellis"; `<main>` with
 * max-width 1200 px. A skip link "Skip to content" is the first focusable element.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <>
      <a href="#main" className={styles['skip']}>
        Skip to content
      </a>
      <header className={styles['header']}>
        <div className={styles['headerInner']}>
          <Link to="/" className={styles['brand']}>
            CardLab
          </Link>
          <span>Brightline Supplies Ltd</span>
          <span className={styles['user']}>Sam Ellis</span>
        </div>
      </header>
      <main id="main" className={styles['main']}>
        {children}
      </main>
    </>
  );
}
