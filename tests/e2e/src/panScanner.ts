/** Runs of 16+ consecutive digits in text (NFR-1, AC-24); same rule as tests/CardLab.TestKit/PanScanner.cs. */
export function findPans(text: string): string[] {
  return text.match(/[0-9]{16,}/g) ?? [];
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
