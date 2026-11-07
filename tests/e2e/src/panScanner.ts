import { notImplemented } from './notImplemented';

/** Runs of 16+ consecutive digits in text (NFR-1, AC-24); same rule as tests/CardLab.TestKit/PanScanner.cs. */
export function findPans(text: string): string[] {
  return text.match(/[0-9]{16,}/g) ?? [];
}

/**
 * 002: occurrences of the seeded linked account's full number (a literal, not "8+ digits": GUIDs contain long digit
 * runs). Implemented in PR-08.
 */
export function findAccountNumbers(text: string): string[] {
  return notImplemented('PR-08', text);
}

/** 002: throws (showing last fours only) when `text` contains a full card number or the full account number. Implemented in PR-08. */
export function expectNoFullNumbers(text: string, where: string): void {
  notImplemented('PR-08', text, where);
}

/** Throws (showing only the last four of each hit) when `text` contains a full card number. */
export function expectNoPan(text: string, where: string): void {
  const hits = findPans(text);
  if (hits.length > 0) {
    const lastFours = hits.map((hit) => `...${hit.slice(-4)}`).join(', ');
    throw new Error(
      `Full card number(s) found in ${where}: ${hits.length.toString()} hit(s) ending ${lastFours}.`,
    );
  }
}
