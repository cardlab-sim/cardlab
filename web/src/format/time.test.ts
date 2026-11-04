import { describe, test } from 'vitest';

describe('formatLondonTime', () => {
  // T-W-2 · PR-02 · web unit
  // Given "2026-10-24T23:30:00Z", "2026-10-25T01:30:00Z" (after the change back to GMT), "2026-03-29T00:30:00Z", "2026-11-04T10:00:00+01:00", When formatLondonTime, Then "25 Oct 2026 00:30", "25 Oct 2026 01:30", "29 Mar 2026 00:30", "4 Nov 2026 09:00"; "nope" throws
  // Catches: browser-zone or UTC display instead of Europe/London; 12-hour clock (AC-38, NFR-6)
  test.todo('T-W-2 formats instants in Europe/London as 25 Oct 2026 00:30');
});
