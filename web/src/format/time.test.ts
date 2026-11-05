import { describe, expect, test } from 'vitest';
import { formatLondonTime } from './time';

describe('formatLondonTime', () => {
  // T-W-2 · PR-02 · web unit
  // Given "2026-10-24T23:30:00Z", "2026-10-25T01:30:00Z" (after the change back to GMT), "2026-03-29T00:30:00Z", "2026-11-04T10:00:00+01:00", When formatLondonTime, Then "25 Oct 2026 00:30", "25 Oct 2026 01:30", "29 Mar 2026 00:30", "4 Nov 2026 09:00"; "nope" throws
  // Catches: browser-zone or UTC display instead of Europe/London; 12-hour clock (AC-38, NFR-6)
  test.each([
    ['2026-10-24T23:30:00Z', '25 Oct 2026 00:30'],
    ['2026-10-25T01:30:00Z', '25 Oct 2026 01:30'],
    ['2026-03-29T00:30:00Z', '29 Mar 2026 00:30'],
    ['2026-11-04T10:00:00+01:00', '4 Nov 2026 09:00'],
    ['2026-09-15T13:05:00Z', '15 Sep 2026 14:05'],
    ['2026-01-02T00:00:00Z', '2 Jan 2026 00:00'],
  ])('T-W-2 formats instants in Europe/London as 25 Oct 2026 00:30: %s', (input, expected) => {
    expect(formatLondonTime(input)).toBe(expected);
  });

  test('T-W-2 rejects an invalid date', () => {
    expect(() => formatLondonTime('nope')).toThrow();
  });
});
