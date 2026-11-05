import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { FakeApiClient } from '../api/fakeApiClient';

import type { CardDetail } from '../api/types';
import { renderWithApi } from '../test/render';
import { CardDetailPage } from './CardDetailPage';

const opsVan: CardDetail = {
  id: 'c1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [{ category: 'fuel', period: 'day', amount: '50.00' }],
};

function renderDetail(fake: FakeApiClient) {
  return renderWithApi(<CardDetailPage />, fake, { route: '/cards/c1', path: '/cards/:id' });
}

async function limitsRegion() {
  return within(await screen.findByRole('region', { name: 'Spend limits' }));
}

describe('CardDetailPage', () => {
  // T-W-3 · PR-07 · web unit
  // Given the limit form, When Save with amount "10.005" (FakeApiClient rejects 400 errors.amount), Then an inline error under Amount (role="alert", aria-describedby on the input) and the limits table unchanged
  // Catches: toast-only or missing validation feedback (AC-9, §4.1)
  test('T-W-3 limit form shows inline errors from the API', async () => {
    const user = userEvent.setup();
    renderDetail(new FakeApiClient({ cards: [opsVan] }));

    const limits = await limitsRegion();
    await user.type(limits.getByLabelText('Amount'), '10.005');
    await user.click(limits.getByRole('button', { name: 'Save' }));

    const error = await limits.findByRole('alert');
    expect(error).toHaveTextContent('Amount must be between 0.00 and 1000000.00');
    expect(limits.getByLabelText('Amount')).toHaveAttribute('aria-describedby', error.id);
    expect(limits.getByLabelText('Amount')).toHaveAttribute('aria-invalid', 'true');
    const rows = limits.getAllByRole('row');
    expect(rows).toHaveLength(2);
    expect(rows[1]).toHaveTextContent('Fuel');
    expect(rows[1]).toHaveTextContent('£50.00');
  });

  // T-W-3 · PR-07 · web unit (added: client-side rejection of empty/non-numeric amounts per the brief's Behaviour)
  // Given the limit form, When Save with an empty or non-numeric amount, Then an inline error and no setLimit call
  // Catches: junk amounts sent to the API
  test.each(['', 'abc'])('T-W-3 limit form rejects amount "%s" client-side', async (typed) => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan] });
    renderDetail(fake);

    const limits = await limitsRegion();
    if (typed !== '') {
      await user.type(limits.getByLabelText('Amount'), typed);
    }
    await user.click(limits.getByRole('button', { name: 'Save' }));

    expect(await limits.findByRole('alert')).toHaveTextContent('Enter the limit as a number');
    expect(fake.calls).not.toContain('setLimit');
  });

  // T-W-3 · PR-07 · web unit
  // Given the simulate form and fake.hold(), When Submit, Then the button shows its pending label and is disabled; after release a Notice shows outcome "Declined", the code limit_exceeded and remaining £0.00, and the new transaction is the first table row
  // Catches: no pending state, result not shown, history not refreshed (AC-22, §4.1)
  test('T-W-3 simulate shows pending state then the decision and refreshes history', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan] });
    renderDetail(fake);
    const simulate = within(await screen.findByRole('region', { name: 'Simulate purchase' }));

    fake.nextDecision = {
      outcome: 'declined',
      reason: 'limit_exceeded',
      transactionId: null,
      limits: [{ category: 'fuel', period: 'day', limit: '50.00', remaining: '0.00' }],
      problems: [],
    };
    const release = fake.hold();
    await user.type(simulate.getByLabelText('Amount'), '60.00');
    await user.selectOptions(simulate.getByLabelText('Category'), 'fuel');
    await user.type(simulate.getByLabelText('Merchant'), 'Shell');
    await user.click(simulate.getByRole('button', { name: 'Submit' }));

    const pending = simulate.getByRole('button', { name: 'Simulating…' });
    expect(pending).toBeDisabled();

    release();
    const result = await simulate.findByRole('status');
    expect(result).toHaveTextContent('Declined');
    expect(result).toHaveTextContent('limit_exceeded');
    expect(result).toHaveTextContent('Fuel day');
    expect(result).toHaveTextContent('£0.00');
    const history = within(screen.getByRole('region', { name: 'Transactions' }));
    await waitFor(() => {
      expect(history.getAllByRole('row')).toHaveLength(2);
    });
    const first = history.getAllByRole('row')[1];
    expect(first).toHaveTextContent('Shell');
    expect(first).toHaveTextContent('£60.00');
    expect(fake.calls.filter((call) => call === 'simulatePurchase')).toHaveLength(1);
  });

  // T-W-3 · PR-07 · web unit
  // Given nextDecision invalid_request with problems [{field:"amount"}], When Submit, Then the error is inline under Amount, no transaction row added
  // Catches: problems list ignored (DEC-14)
  test('T-W-3 simulate shows validation problems inline', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan] });
    renderDetail(fake);
    const simulate = within(await screen.findByRole('region', { name: 'Simulate purchase' }));

    fake.nextDecision = {
      outcome: 'declined',
      reason: 'invalid_request',
      transactionId: null,
      limits: [],
      problems: [{ field: 'amount', message: 'Amount must be greater than 0.00.' }],
    };
    await user.type(simulate.getByLabelText('Amount'), '0');
    await user.type(simulate.getByLabelText('Merchant'), 'Shell');
    await user.click(simulate.getByRole('button', { name: 'Submit' }));

    const error = await simulate.findByRole('alert');
    expect(error).toHaveTextContent('Amount must be greater than 0.00.');
    expect(simulate.getByLabelText('Amount')).toHaveAttribute('aria-describedby', error.id);
    expect(simulate.queryByRole('status')).toBeNull();
    const history = within(screen.getByRole('region', { name: 'Transactions' }));
    expect(history.getByText('No transactions yet.')).toBeInTheDocument();
  });

  // T-W-8 · PR-07 · web unit
  // Given an active card, When Freeze is pressed, Then status badge "Frozen", button now "Unfreeze", and the audit list's first item is the freeze entry with actor "Sam Ellis" and London time
  // Catches: stale status/audit after an action (AC-1, AC-23)
  test('T-W-8 freeze updates status, button and audit', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-07-01T12:00:00Z') });
    try {
      const user = userEvent.setup();
      renderDetail(new FakeApiClient({ cards: [opsVan] }));

      expect(await screen.findByRole('heading', { name: 'Ops van' })).toBeInTheDocument();
      expect(screen.getByText('•••• 0001')).toBeInTheDocument();
      expect(screen.getByText('Active')).toBeInTheDocument();
      await user.click(screen.getByRole('button', { name: 'Freeze' }));

      expect(await screen.findByRole('button', { name: 'Unfreeze' })).toBeInTheDocument();
      expect(screen.getByText('Frozen')).toBeInTheDocument();
      expect(screen.queryByText('Active')).toBeNull();
      const audit = within(screen.getByRole('region', { name: 'Audit' }));
      const items = audit.getAllByRole('listitem');
      expect(items).toHaveLength(1);
      expect(items[0]).toHaveTextContent('Card frozen');
      expect(items[0]).toHaveTextContent('Sam Ellis');
      expect(items[0]).toHaveTextContent('1 Jul 2026 13:00');
      expect(items[0]).toHaveTextContent('active → frozen');
    } finally {
      vi.useRealTimers();
    }
  });

  // T-W-9 · PR-07 · web unit
  // Given a card with a £1,234.56 Fuel/Day limit and a transaction at 2026-10-24T23:30:00Z, When rendered, Then the limit shows "£1,234.56" and the transaction time "25 Oct 2026 00:30"; Remove "Remove Fuel day limit" removes the row
  // Catches: raw amounts/UTC times on the page (AC-38), missing remove (AC-8)
  test('T-W-9 renders limits and transactions with UK formatting and removes a limit', async () => {
    const user = userEvent.setup();
    const card: CardDetail = {
      ...opsVan,
      limits: [
        { category: 'fuel', period: 'day', amount: '1234.56' },
        { category: 'travel', period: 'month', amount: '500.00' },
      ],
    };
    const fake = new FakeApiClient({
      cards: [card],
      transactions: {
        c1: [
          {
            id: 't1',
            occurredAt: '2026-10-24T23:30:00Z',
            amount: '12.50',
            category: 'fuel',
            merchant: 'Shell',
            outcome: 'approved',
            reason: 'approved',
          },
        ],
      },
    });
    renderDetail(fake);

    const limits = await limitsRegion();
    expect(limits.getByText('£1,234.56')).toBeInTheDocument();
    const history = within(screen.getByRole('region', { name: 'Transactions' }));
    expect(history.getByText('25 Oct 2026 00:30')).toBeInTheDocument();

    await user.click(limits.getByRole('button', { name: 'Remove Fuel day limit' }));

    await waitFor(() => {
      expect(limits.queryByText('£1,234.56')).toBeNull();
    });
    expect(limits.getByText('£500.00')).toBeInTheDocument();
    expect(limits.getByRole('button', { name: 'Remove Travel month limit' })).toBeInTheDocument();
  });

  // T-W-9 · PR-07 · web unit (added: a failed removal must be visible, not an unhandled rejection)
  // Given removeLimit rejecting, When Remove is pressed, Then an inline error Notice and the limit stays
  // Catches: silent failure of Remove (§4.1 inline feedback)
  test('T-W-9 remove failure shows an inline error', async () => {
    class FailingRemove extends FakeApiClient {
      override removeLimit(): Promise<void> {
        return Promise.reject(new Error('boom'));
      }
    }
    const user = userEvent.setup();
    renderDetail(new FailingRemove({ cards: [opsVan] }));

    const limits = await limitsRegion();
    await user.click(limits.getByRole('button', { name: 'Remove Fuel day limit' }));

    expect(await limits.findByRole('alert')).toHaveTextContent('Could not remove the limit');
    expect(limits.getByText('£50.00')).toBeInTheDocument();
  });

  // T-W-10 · PR-07 · web unit
  // Given getCard rejecting with ApiError 404, When rendered at /cards/unknown, Then a "Card not found" notice with a link back to the card list
  // Catches: crash or blank page for unknown ids (design §9)
  test('T-W-10 unknown card shows a not-found notice', async () => {
    renderWithApi(<CardDetailPage />, new FakeApiClient({ cards: [opsVan] }), {
      route: '/cards/unknown',
      path: '/cards/:id',
    });

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent('Card not found');
    expect(within(notice).getByRole('link', { name: 'Back to the card list' })).toHaveAttribute(
      'href',
      '/',
    );
  });
});
