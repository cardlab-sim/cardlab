// Seed constants (complete in PR-00); must match src/CardLab.Api/appsettings.json and tests/CardLab.TestKit/Seed.cs.
// Fictional data. Card numbers are used only in API calls from tests (never typed into the UI; NFR-1).

export interface SeedCard {
  name: string;
  cardholderName: string;
  cardNumber: string;
  lastFour: string;
}

export const seed = {
  accountName: 'Brightline Supplies Ltd',
  adminName: 'Sam Ellis',
  cards: {
    opsVan: {
      name: 'Ops van',
      cardholderName: 'Priya Nair',
      cardNumber: '9999000100000001',
      lastFour: '0001',
    },
    fieldSales: {
      name: 'Field sales',
      cardholderName: 'Tom Okafor',
      cardNumber: '9999000100000002',
      lastFour: '0002',
    },
    marketing: {
      name: 'Marketing',
      cardholderName: 'Leah Brooks',
      cardNumber: '9999000100000003',
      lastFour: '0003',
    },
  } satisfies Record<string, SeedCard>,
  unknownCardNumber: '9999000199999999',
} as const;
