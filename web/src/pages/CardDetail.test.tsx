import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { FakeApiClient } from '../api/fakeApiClient';
import type { AuditEntry, CardDetail } from '../api/types';
import { renderWithApi } from '../test/render';
import { CardDetailPage } from './CardDetailPage';

const opsVan: CardDetail = {
  id: 'c1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [{ category: 'fuel', period: 'day', amount: '50.00' }],
  balanceOwed: '0.00',
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
    expect(pending).toHaveAttribute('aria-disabled', 'true');

    release();
    const result = await simulate.findByRole('status');
    expect(result).toHaveTextContent('Declined');
    expect(result).toHaveTextContent('limit_exceeded');
    expect(result).toHaveTextContent('Fuel day');
    expect(result).toHaveTextContent('£0.00');
    const history = within(screen.getByRole('region', { name: 'Activity' }));
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
    const history = within(screen.getByRole('region', { name: 'Activity' }));
    expect(history.getByText('No activity yet.')).toBeInTheDocument();
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
      expect(items[0]).toHaveTextContent('Active → Frozen');
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
    const fake = new FakeApiClient({ cards: [card] });
    await fake.simulatePurchase('c1', {
      amount: '12.50',
      category: 'fuel',
      merchant: 'Shell',
      timestamp: '2026-10-24T23:30:00Z',
    });
    renderDetail(fake);

    const limits = await limitsRegion();
    expect(limits.getByText('£1,234.56')).toBeInTheDocument();
    const history = within(await screen.findByRole('region', { name: 'Activity' }));
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

  // T-W-3 · PR-07 · web unit (added in the fix round: B1)
  // Given audit entries in the API's format (design §6), When rendered, Then the list is newest first with formatted values
  // Catches: crash on "Fuel/Day/50.00" values; list order changed; raw wire values shown (AC-23)
  test('T-W-3 audit list shows API-format values newest first (AC-23)', async () => {
    const entry = (
      n: number,
      action: AuditEntry['action'],
      oldValue: string | null,
      newValue: string | null,
    ): AuditEntry => ({
      id: `a${n}`,
      atUtc: `2026-07-01T12:0${n}:00Z`,
      actorName: 'Sam Ellis',
      action,
      oldValue,
      newValue,
    });
    renderDetail(
      new FakeApiClient({
        cards: [opsVan],
        audit: {
          c1: [
            entry(3, 'limit_removed', 'Fuel/Day/80.00', null),
            entry(2, 'limit_changed', 'Fuel/Day/50.00', 'Fuel/Day/80.00'),
            entry(1, 'limit_set', null, 'MealsAndEntertainment/Week/10.00'),
            entry(0, 'unfreeze', 'frozen', 'active'),
            entry(4, 'limit_set', null, 'not a known shape'),
          ],
        },
      }),
    );

    const audit = within(await screen.findByRole('region', { name: 'Audit' }));
    const items = audit.getAllByRole('listitem');
    expect(items).toHaveLength(5);
    expect(items[0]).toHaveTextContent('Limit removed');
    expect(items[0]).toHaveTextContent('Sam Ellis');
    expect(items[0]).toHaveTextContent('1 Jul 2026 13:03');
    expect(items[0]).toHaveTextContent('Fuel day £80.00 → —');
    expect(items[1]).toHaveTextContent('Limit changed');
    expect(items[1]).toHaveTextContent('Fuel day £50.00 → Fuel day £80.00');
    expect(items[2]).toHaveTextContent('Limit set');
    expect(items[2]).toHaveTextContent('— → Meals & Entertainment week £10.00');
    expect(items[3]).toHaveTextContent('Frozen → Active');
    expect(items[4]).toHaveTextContent('— → not a known shape');
  });

  // T-W-9 · PR-07 · web unit (added in the fix round: M1, M2)
  // Given the limit form, When Save a new value for Fuel/Day then a new pair, Then the table shows the replaced and the new row, the input is cleared and a success notice is shown
  // Catches: no refetch after Save (AC-6, AC-7); no confirmation (§4.1)
  test('T-W-9 saving a limit adds then replaces the row', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan] });
    renderDetail(fake);

    const limits = await limitsRegion();
    await user.selectOptions(limits.getByLabelText('Category'), 'fuel');
    await user.selectOptions(limits.getByLabelText('Period'), 'day');
    await user.type(limits.getByLabelText('Amount'), '80.00');
    await user.click(limits.getByRole('button', { name: 'Save' }));

    expect(await limits.findByText('£80.00')).toBeInTheDocument();
    expect(limits.queryByText('£50.00')).toBeNull();
    expect(limits.getByRole('status')).toHaveTextContent('Limit saved: Fuel day £80.00');
    expect(limits.getByLabelText('Amount')).toHaveValue('');

    await user.selectOptions(limits.getByLabelText('Category'), 'travel');
    await user.selectOptions(limits.getByLabelText('Period'), 'month');
    await user.type(limits.getByLabelText('Amount'), '500');
    await user.click(limits.getByRole('button', { name: 'Save' }));

    expect(await limits.findByText('£500.00')).toBeInTheDocument();
    expect(limits.getAllByRole('row')).toHaveLength(3);
    expect(limits.getByText('£80.00')).toBeInTheDocument();
    expect(limits.getByLabelText('Amount')).toHaveValue('');
    expect(fake.calls.filter((call) => call === 'setLimit')).toHaveLength(2);
  });

  // T-W-9 · PR-07 · web unit (added in the fix round: M2)
  // Given fake.hold(), When Remove is pressed, Then the button shows "Removing…" and is disabled; after release a success notice is shown
  // Catches: no pending state or confirmation on Remove (§4.1)
  test('T-W-9 remove shows a pending state then a confirmation', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan] });
    renderDetail(fake);
    const limits = await limitsRegion();
    await limits.findByText('£50.00');

    const release = fake.hold();
    await user.click(limits.getByRole('button', { name: 'Remove Fuel day limit' }));
    expect(limits.getByRole('button', { name: 'Removing…' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );

    release();
    expect(await limits.findByRole('status')).toHaveTextContent('Limit removed: Fuel day');
    await waitFor(() => {
      expect(limits.queryByText('£50.00')).toBeNull();
    });
  });

  // T-W-8 · PR-07 · web unit (added in the fix round: M2)
  // Given an active card, When Freeze is pressed, Then a success notice says the card was frozen
  // Catches: no confirmation after Freeze (§4.1)
  test('T-W-8 freeze confirms with a notice', async () => {
    const user = userEvent.setup();
    renderDetail(new FakeApiClient({ cards: [opsVan] }));

    await user.click(await screen.findByRole('button', { name: 'Freeze' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Card frozen');
  });

  // T-W-8 · PR-07 · web unit (added in the fix round: m3)
  // Given a slow refetch after Freeze, When Freeze is pressed, Then the button stays "Freezing…" and disabled until the new status is shown
  // Catches: a stale, enabled "Freeze" button while the refetch is in flight
  test('T-W-8 freeze stays pending until the refetch lands', async () => {
    const user = userEvent.setup();
    const gated = new GatedCardClient({ cards: [opsVan] }, 2);
    renderDetail(gated);

    await user.click(await screen.findByRole('button', { name: 'Freeze' }));

    await screen.findByRole('button', { name: 'Freezing…' });
    expect(screen.getByRole('button', { name: 'Freezing…' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    gated.release();
    expect(await screen.findByRole('button', { name: 'Unfreeze' })).toBeEnabled();
  });

  // T-W-8 · PR-07 · web unit (added in the fix round: m1)
  // Given a slow refetch after Save and a fast one after Freeze, When both complete, Then the newest data (Frozen) wins
  // Catches: an older response overwriting a newer one
  test('T-W-8 an older refetch does not overwrite a newer one', async () => {
    const user = userEvent.setup();
    const gated = new GatedCardClient({ cards: [opsVan] }, 2);
    renderDetail(gated);
    const limits = await limitsRegion();

    await user.type(limits.getByLabelText('Amount'), '80.00');
    await user.click(limits.getByRole('button', { name: 'Save' }));
    await waitFor(() => {
      expect(gated.getCardCalls).toBe(2);
    });
    await user.click(screen.getByRole('button', { name: 'Freeze' }));
    expect(await screen.findByText('Frozen')).toBeInTheDocument();

    gated.release();
    await waitFor(() => {
      expect(limits.getByRole('button', { name: 'Save' })).toBeEnabled();
    });
    expect(screen.getByText('Frozen')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Unfreeze' })).toBeInTheDocument();
  });

  // T-W-8 · PR-07 · web unit (added in the fix round: m2)
  // Given a refetch that fails after a successful Freeze, When Freeze is pressed, Then the page keeps its data and shows an alert
  // Catches: the whole page replaced by a load error after a successful action
  test('T-W-8 a failed refetch keeps the page and shows an alert', async () => {
    class AuditFailsAfterFirst extends FakeApiClient {
      private auditCalls = 0;
      override listAudit(id: string): Promise<AuditEntry[]> {
        this.auditCalls += 1;
        return this.auditCalls > 1 ? Promise.reject(new Error('boom')) : super.listAudit(id);
      }
    }
    const user = userEvent.setup();
    renderDetail(new AuditFailsAfterFirst({ cards: [opsVan] }));

    await user.click(await screen.findByRole('button', { name: 'Freeze' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not refresh the card');
    expect(screen.getByRole('heading', { name: 'Ops van' })).toBeInTheDocument();
  });
});

/** FakeApiClient whose `gatedCall`-th getCard returns the state at call time but only after `release()`. */
class GatedCardClient extends FakeApiClient {
  getCardCalls = 0;
  private open: () => void = () => {};
  private readonly latch = new Promise<void>((resolve) => {
    this.open = resolve;
  });

  constructor(
    seed: ConstructorParameters<typeof FakeApiClient>[0],
    private readonly gatedCall: number,
  ) {
    super(seed);
  }

  release(): void {
    this.open();
  }

  override async getCard(id: string): Promise<CardDetail> {
    this.getCardCalls += 1;
    const snapshot = await super.getCard(id);
    if (this.getCardCalls === this.gatedCall) {
      await this.latch;
    }
    return snapshot;
  }
}

describe('CardDetailPage (002)', () => {
  // T-W-8 · PR-07 · web unit
  // Given a card with balanceOwed "150.50", a purchase and a completed payment, When rendered, Then the heading shows "Balance owed £20.00" (the opening £150.50 was paid, a £20.00 purchase followed); the Activity table (caption "Transaction history") lists the payment with a Payment type badge, Completed state badge and a link to its record, and the purchase with its merchant, category, outcome and reason; the Payments section lists every reference with status, state and origin badges
  // Catches: the admin unable to see the balance or tell payments from purchases (AC-1, AC-2, R-13, DEC-12)
  test('T-W-8 shows balance, activity and payments', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-24T23:30:00Z') });
    try {
      const fake = new FakeApiClient({ cards: [{ ...opsVan, balanceOwed: '150.50' }] });
      const paid = await fake.createQuote('c1');
      await fake.confirmPayment('c1', paid.reference);
      fake.addPurchase('c1', '20.00');
      await fake.simulatePurchase('c1', { amount: '12.50', category: 'fuel', merchant: 'Shell' });
      const open = await fake.createQuote('c1');
      renderDetail(fake);

      expect(await screen.findByText('Balance owed £32.50')).toBeInTheDocument();

      const activity = within(screen.getByRole('table', { name: 'Transaction history' }));
      const rows = activity.getAllByRole('row').slice(1);
      expect(rows).toHaveLength(4);
      // Newest first: the Shell purchase, the £20.00 purchase, the completed payment, the opening purchase.
      expect(cellTexts(rows[0])).toEqual([
        '25 Oct 2026 00:30',
        'Purchase',
        'Shell',
        'Fuel',
        '£12.50',
        'Approved',
        'Approved',
      ]);
      expect(cellTexts(rows[1])[4]).toBe('£20.00');
      const payment = cellTexts(rows[2]);
      expect(payment[1]).toBe('Payment');
      expect(payment[2]).toBe(`Payment ${paid.reference}`);
      expect(payment[3]).toBe('—');
      expect(payment[4]).toBe('£150.50');
      expect(payment[5]).toBe('Completed');
      expect(
        within(rows[2] as HTMLElement).getByRole('link', { name: `Payment ${paid.reference}` }),
      ).toHaveAttribute('href', `/cards/c1/payments/${paid.reference}`);
      expect(cellTexts(rows[3])[4]).toBe('£150.50');

      const payments = within(screen.getByRole('table', { name: 'Payment references' }));
      const paymentRows = payments.getAllByRole('row').slice(1);
      expect(paymentRows).toHaveLength(2);
      expect(within(paymentRows[0] as HTMLElement).getByRole('link')).toHaveTextContent(
        open.reference,
      );
      expect(paymentRows[0]).toHaveTextContent('Open');
      expect(paymentRows[0]).toHaveTextContent('£32.50');
      expect(paymentRows[0]).toHaveTextContent('Cardholder view');
      expect(within(paymentRows[1] as HTMLElement).getByRole('link')).toHaveTextContent(
        paid.reference,
      );
      expect(paymentRows[1]).toHaveTextContent('Confirmed');
      expect(paymentRows[1]).toHaveTextContent('Completed');
    } finally {
      vi.useRealTimers();
    }
  });

  // T-W-8 · PR-07 · web unit (added: DEC-15 smoke payments are told apart)
  // Given a smoke-origin payment in activity and in the payments list, When rendered, Then the Reason cell and the Payments row carry the "origin: smoke" badge
  // Catches: smoke payments indistinguishable from cardholder ones (DEC-15)
  test('T-W-8 marks smoke payments with an origin badge', async () => {
    const fake = new FakeApiClient({ cards: [{ ...opsVan, balanceOwed: '1.00' }] });
    fake.withQuoteOrigin('deploy_smoke');
    const quote = await fake.createQuote('c1');
    await fake.confirmPayment('c1', quote.reference);
    renderDetail(fake);

    const activity = within(await screen.findByRole('table', { name: 'Transaction history' }));
    expect(activity.getByText('origin: smoke')).toBeInTheDocument();
    const payments = within(screen.getByRole('table', { name: 'Payment references' }));
    expect(payments.getByText('origin: smoke')).toBeInTheDocument();
  });

  // T-W-8 · PR-07 · web unit
  // Given the payments list contains a pending payment, When rendered, Then checkPayment is called once for it and the page re-reads card, activity and payments
  // Catches: admin detail not resolving pending payments on open (AC-34, R-11, DEC-13)
  test('T-W-8 auto-checks a pending payment on open', async () => {
    const fake = new FakeApiClient({ cards: [{ ...opsVan, balanceOwed: '150.50' }] });
    const quote = await fake.createQuote('c1');
    fake.withBank('timeout_debit_applied');
    await fake.confirmPayment('c1', quote.reference);
    renderDetail(fake);

    expect(await screen.findByText('Balance owed £0.00')).toBeInTheDocument();

    const payments = within(screen.getByRole('table', { name: 'Payment references' }));
    expect(payments.getAllByRole('row')[1]).toHaveTextContent('Completed');
    expect(fake.calls.filter((call) => call === 'checkPayment')).toHaveLength(1);
    expect(fake.calls.filter((call) => call === 'getCard')).toHaveLength(2);
    expect(fake.calls.filter((call) => call === 'listActivity')).toHaveLength(2);
    expect(fake.calls.filter((call) => call === 'listPaymentRecords')).toHaveLength(2);
  });

  // T-W-8 · PR-07 · web unit (added: a pending payment the bank has not answered is checked once, not in a loop)
  // Given a pending payment and a bank that does not answer, When rendered, Then checkPayment is called once and the payment stays Pending
  // Catches: an endless check loop on an unanswered bank (DEC-13, AC-16)
  test('T-W-8 does not re-check an unanswered pending payment', async () => {
    const fake = new FakeApiClient({ cards: [{ ...opsVan, balanceOwed: '150.50' }] });
    const quote = await fake.createQuote('c1');
    fake.withBank('timeout_debit_applied');
    await fake.confirmPayment('c1', quote.reference);
    fake.withStatusCheck('does_not_answer');
    renderDetail(fake);

    const payments = within(await screen.findByRole('table', { name: 'Payment references' }));
    await waitFor(() => {
      expect(fake.calls.filter((call) => call === 'listPaymentRecords')).toHaveLength(2);
    });
    // The refetch has started; let its state render so a second auto-check (if the guard were missing) would run.
    await act(() => new Promise((resolve) => setTimeout(resolve, 50)));
    expect(fake.calls.filter((call) => call === 'listPaymentRecords')).toHaveLength(2);
    expect(payments.getAllByRole('row')[1]).toHaveTextContent('Pending');
    expect(fake.calls.filter((call) => call === 'checkPayment')).toHaveLength(1);
  });

  // T-W-8 · PR-07 · web unit (added: review F3)
  // Given a pending payment whose auto check is still in flight, When the admin freezes the card meanwhile and the check then settles the payment, Then the page still shows the completed payment and balance £0.00
  // Catches: a user action during the check making the page skip the post-check refetch (AC-34, R-11)
  test('T-W-8 a user action during the auto check does not hide the settled payment', async () => {
    const user = userEvent.setup();
    const fake = new SlowCheckFake({ cards: [{ ...opsVan, balanceOwed: '150.50' }] });
    const quote = await fake.createQuote('c1');
    fake.withBank('timeout_debit_applied');
    await fake.confirmPayment('c1', quote.reference);
    renderDetail(fake);

    await user.click(await screen.findByRole('button', { name: 'Freeze' }));
    expect(await screen.findByText('Frozen')).toBeInTheDocument();
    fake.releaseCheck();

    expect(await screen.findByText('Balance owed £0.00')).toBeInTheDocument();
    const payments = within(screen.getByRole('table', { name: 'Payment references' }));
    expect(payments.getAllByRole('row')[1]).toHaveTextContent('Completed');
  });
});

function cellTexts(row: HTMLElement | undefined): (string | null)[] {
  return within(row as HTMLElement)
    .getAllByRole('cell')
    .map((cell) => cell.textContent);
}

/** FakeApiClient whose status check waits for `releaseCheck()`. */
class SlowCheckFake extends FakeApiClient {
  private open: () => void = () => {};
  private readonly latch = new Promise<void>((resolve) => {
    this.open = resolve;
  });

  releaseCheck(): void {
    this.open();
  }

  override async checkPayment(
    id: string,
    reference: string,
  ): ReturnType<FakeApiClient['checkPayment']> {
    await this.latch;
    return super.checkPayment(id, reference);
  }
}
