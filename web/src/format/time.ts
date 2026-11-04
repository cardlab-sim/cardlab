import { notImplemented } from '../notImplemented';

/**
 * Formats an ISO-8601 instant in Europe/London (NFR-6, AC-38): `"2026-10-24T23:30:00Z"` → `"25 Oct 2026 00:30"`
 * (24-hour clock, day without leading zero, short English month), whatever the browser's time zone.
 * Use `Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/London', ... })` parts and assemble the exact pattern.
 * @param iso - Instant with offset or Z.
 * @returns The display text.
 * @throws Error when `iso` is not a valid date.
 * Implemented in PR-02.
 */
export function formatLondonTime(iso: string): string {
  return notImplemented('PR-02', iso);
}
