import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { ApiError } from '../api/apiClient';
import { FakeApiClient } from '../api/fakeApiClient';
import type { CardDetail } from '../api/types';
import { renderWithApi } from '../test/render';
import { CardListPage } from './CardListPage';

const opsVan: CardDetail = {
  id: 'ops-1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [],
};
const marketing: CardDetail = {
  id: 'mkt-2',
  name: 'Marketing',
  cardholderName: 'Lee Park',
  lastFour: '0002',
  status: 'frozen',
  limits: [],
};

describe('CardListPage', () => {
  // T-W-7 · PR-02 · web unit
  // Given FakeApiClient with Ops van (active) and Marketing (frozen), When rendered at /, Then a table row per card with name, cardholder, "0001", a status badge with the text "Active"/"Frozen", and a link "Open Ops van" to /cards/{id}
  // Catches: status shown by colour only; missing navigation (R-8, §4.1)
  test('T-W-7 lists cards with text status badges and open links', async () => {
    renderWithApi(<CardListPage />, new FakeApiClient({ cards: [opsVan, marketing] }));

    expect(screen.getByRole('heading', { name: 'Cards' })).toBeInTheDocument();
    const opsRow = (await screen.findByRole('cell', { name: 'Ops van' })).closest('tr');
    expect(opsRow).not.toBeNull();
    const ops = within(opsRow as HTMLElement);
    expect(ops.getByText('Dana Fox')).toBeInTheDocument();
    expect(ops.getByText('0001')).toBeInTheDocument();
    expect(ops.getByText('Active')).toBeInTheDocument();
    expect(ops.getByRole('link', { name: 'Open Ops van' })).toHaveAttribute('href', '/cards/ops-1');

    const mktRow = screen.getByRole('cell', { name: 'Marketing' }).closest('tr') as HTMLElement;
    expect(within(mktRow).getByText('Frozen')).toBeInTheDocument();
    expect(within(mktRow).getByText('0002')).toBeInTheDocument();
    expect(within(mktRow).getByRole('link', { name: 'Open Marketing' })).toHaveAttribute(
      'href',
      '/cards/mkt-2',
    );
    expect(screen.getAllByRole('row')).toHaveLength(3);
  });

  // T-W-7 · PR-02 · web unit
  // Given listCards rejecting with ApiError 503, When rendered, Then an error Notice (role="alert") and no table
  // Catches: blank page on API failure (design §9)
  test('T-W-7 shows an error notice when the list fails', async () => {
    const client = new FakeApiClient({ cards: [opsVan] });
    client.listCards = () => Promise.reject(new ApiError(503, null));
    renderWithApi(<CardListPage />, client);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load cards');
    expect(screen.queryByRole('table')).toBeNull();
  });

  test('T-W-7 shows loading text until the list arrives', async () => {
    const client = new FakeApiClient({ cards: [opsVan] });
    const release = client.hold();
    renderWithApi(<CardListPage />, client);

    expect(screen.getByText('Loading cards…')).toBeInTheDocument();
    expect(screen.queryByRole('table')).toBeNull();
    release();
    expect(await screen.findByRole('table', { name: 'Card list' })).toBeInTheDocument();
    expect(screen.queryByText('Loading cards…')).toBeNull();
  });
});
