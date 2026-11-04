import { notImplemented } from '../notImplemented';

/**
 * Route `/cards/:id` (§4.1): loads card, transactions and audit; heading with name, "•••• 0001" and status badge +
 * StatusSection; two columns at ≥ 1024 px (left LimitsSection + AuditSection, right SimulateSection +
 * TransactionsSection), stacked below. After any successful action, refetches card, transactions and audit.
 * Unknown card (404) → Notice "Card not found" with a link back to the list.
 * Implemented in PR-07 (with CardDetailPage.module.css and pages/cardDetail/*).
 */
export function CardDetailPage() {
  return notImplemented('PR-07');
}
