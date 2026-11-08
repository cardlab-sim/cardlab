import { useEffect, useState } from 'react';
import { Outlet, useParams } from 'react-router';
import { useApi } from '../api/ApiContext';
import styles from './AppShell.module.css';
import cardholderStyles from '../pages/cardholder/Cardholder.module.css';

/**
 * 002 cardholder page frame (DEC-11, §4.1): skip link "Skip to content" first; `<header>` with "CardLab", "Brightline
 * Supplies Ltd", the card's cardholder name (looked up from `listCards()` by the `:id` route param; never the admin's
 * name) and the text label "Cardholder view"; `<main id="main">` single column, max-width 720 px, rendering `<Outlet />`.
 * Reuses `AppShell.module.css` classes plus a 720 px main. While the name is unknown (loading, unknown card) the name is
 * omitted.
 */
export function CardholderShell() {
  const api = useApi();
  const { id } = useParams();
  const [name, setName] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api
      .listCards()
      .then((cards) => {
        if (!cancelled) {
          setName(cards.find((card) => card.id === id)?.cardholderName ?? null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setName(null);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [api, id]);

  return (
    <>
      <a href="#main" className={styles['skip']}>
        Skip to content
      </a>
      <header className={styles['header']}>
        <div className={`${styles['headerInner']} ${cardholderStyles['frame']}`}>
          <span className={styles['brand']}>CardLab</span>
          <span>Brightline Supplies Ltd</span>
          {name !== null && <span>{name}</span>}
          <span className={styles['user']}>Cardholder view</span>
        </div>
      </header>
      <main id="main" className={`${styles['main']} ${cardholderStyles['main']}`}>
        <Outlet />
      </main>
    </>
  );
}
