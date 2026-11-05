/**
 * Formats an ISO-8601 instant in Europe/London (NFR-6, AC-38): `"2026-10-24T23:30:00Z"` → `"25 Oct 2026 00:30"`
 * (24-hour clock, day without leading zero, short English month), whatever the browser's time zone.
 * @param iso - Instant with offset or Z.
 * @returns The display text.
 * @throws Error when `iso` is not a valid date.
 */
export function formatLondonTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    throw new Error(`Not a valid date: ${iso}`);
  }
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/London',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((p) => p.type === type)?.value ?? '';
  const month = part('month') === 'Sept' ? 'Sep' : part('month');
  return `${part('day')} ${month} ${part('year')} ${part('hour')}:${part('minute')}`;
}
