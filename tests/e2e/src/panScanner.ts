import { notImplemented } from './notImplemented';

/** Runs of 16+ consecutive digits in text (NFR-1, AC-24); same rule as tests/CardLab.TestKit/PanScanner.cs. Implemented in PR-10. */
export function findPans(text: string): string[] {
  return notImplemented('PR-10', text);
}

/** Throws (showing only the last four of each hit) when `text` contains a full card number. Implemented in PR-10. */
export function expectNoPan(text: string, where: string): void {
  notImplemented('PR-10', text, where);
}
