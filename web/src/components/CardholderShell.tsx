import { notImplemented } from '../notImplemented';

/**
 * 002 cardholder page frame (DEC-11, §4.1): skip link "Skip to content" first; `<header>` with "CardLab", "Brightline
 * Supplies Ltd", the card's cardholder name (looked up from `listCards()` by the `:id` route param; never the admin's
 * name) and the text label "Cardholder view"; `<main id="main">` single column, max-width 720 px, rendering `<Outlet />`.
 * Reuses `AppShell.module.css` classes plus a 720 px main. Implemented in PR-03.
 */
export function CardholderShell(): never {
  return notImplemented('PR-03');
}
