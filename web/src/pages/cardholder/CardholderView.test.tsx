import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { FakeApiClient } from '../../api/fakeApiClient';
import type { CardDetail } from '../../api/types';
import { CardholderShell } from '../../components/CardholderShell';
import { renderWithApi } from '../../test/render';
import { CardholderViewPage } from './CardholderViewPage';

const opsVan: CardDetail = {
  id: 'ops-1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [],
  balanceOwed: '0.00',
};

const route = { route: '/cardholder/ops-1', path: '/cardholder/:id' };

afterEach(() => {
  vi.useRealTimers();
});

function names(role: 'heading' | 'button' | 'link'): string[] {
  return screen.getAllByRole(role).map((el) => el.textContent);
}

describe('CardholderViewPage', () => {
  // T-W-2 · PR-03 · web unit
  // Given FakeApiClient with Ops van owing £150.50, When rendered at /cardholder/:id, Then heading "Ops van" with "•••• 0001", balance "£150.50", a "Pay full balance" link to /cardholder/:id/pay, the linked account name, "ending 7731", available balance and authority text, both activity tables, and the Fake bank section last in DOM order
  // Catches: missing balance/account or the fake-bank control placed above the pay action (AC-4, R-15, CQ-8)
  test('T-W-2 shows balance, account, activity and the fake bank section last', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-07T10:00:00Z'));
    const fake = new FakeApiClient({ cards: [opsVan], purchases: { 'ops-1': ['100.00'] } });
    await fake.confirmPayment('ops-1', (await fake.createQuote('ops-1')).reference);
    fake.addPurchase('ops-1', '150.50');
    renderWithApi(<CardholderViewPage />, fake, route);

    expect(await screen.findByRole('heading', { level: 1, name: /Ops van/ })).toHaveTextContent(
      'Ops van •••• 0001',
    );
    const balance = screen.getByRole('region', { name: 'Balance' });
    expect(within(balance).getByText('£150.50')).toBeInTheDocument();
    expect(within(balance).getByRole('link', { name: 'Pay full balance' })).toHaveAttribute(
      'href',
      '/cardholder/ops-1/pay',
    );

    const account = screen.getByRole('region', { name: 'Linked current account' });
    expect(account).toHaveTextContent('Brightline Supplies Ltd current account');
    expect(account).toHaveTextContent('ending 7731');
    expect(account).toHaveTextContent('£24,900.00');
    expect(account).toHaveTextContent(
      'Standing authority from Brightline Supplies Ltd to pay this card from this account.',
    );
    expect(account).not.toHaveTextContent('51907731');

    const cardTable = screen.getByRole('table', { name: 'Recent card activity' });
    const cardRows = within(cardTable).getAllByRole('row');
    expect(cardRows).toHaveLength(4);
    expect(
      within(cardRows[1] as HTMLElement)
        .getAllByRole('cell')
        .map((c) => c.textContent),
    ).toEqual(['7 Nov 2026 10:00', 'Card purchase', '£150.50', 'Purchase', 'Approved']);
    expect(
      within(cardRows[2] as HTMLElement)
        .getAllByRole('cell')
        .map((c) => c.textContent),
    ).toEqual([
      '7 Nov 2026 10:00',
      expect.stringMatching(/^Payment PAY-\d+$/),
      '£100.00',
      'Payment',
      'Completed',
    ]);

    const accountTable = screen.getByRole('table', { name: 'Current account recent activity' });
    const accountCells = within(within(accountTable).getAllByRole('row')[1] as HTMLElement)
      .getAllByRole('cell')
      .map((c) => c.textContent);
    expect(accountCells).toEqual([
      '7 Nov 2026 10:00',
      expect.stringMatching(/^PAY-\d+$/),
      '£100.00',
    ]);

    const headings = names('heading');
    expect(headings).toEqual([
      'Ops van •••• 0001',
      'Balance',
      'Linked current account',
      'Fake bank (simulation control)',
    ]);
    const panel = await screen.findByRole('region', { name: 'Fake bank (simulation control)' });
    for (const other of [balance, account, cardTable, accountTable]) {
      expect(other.compareDocumentPosition(panel) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  // T-W-2 · PR-03 · web unit
  // Given balance £0.00 / £1,000,000.01, When rendered, Then no pay button and the server's availabilityMessage (W-7 / W-8) verbatim
  // Catches: a pay button offered when nothing can be paid (AC-5, AC-24)
  test.each([
    ['0.00', 'Nothing to pay. Your card balance is £0.00.'],
    [
      '1000000.01',
      'This balance cannot be paid here. Payments above £1,000,000.00 are not available online. No money has moved. Your card balance is £1,000,000.01. Contact your account administrator.',
    ],
  ])('T-W-2 shows W-7 or W-8 instead of the pay action: %s', async (owed, message) => {
    const fake = new FakeApiClient({
      cards: [opsVan],
      purchases: { 'ops-1': owed === '0.00' ? [] : [owed] },
    });
    renderWithApi(<CardholderViewPage />, fake, route);

    const balance = await screen.findByRole('region', { name: 'Balance' });
    expect(within(balance).getByText(message)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Pay full balance' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Pay full balance' })).toBeNull();
    expect(within(balance).queryByRole('button', { name: 'Check status' })).toBeNull();
  });

  // T-W-2 · PR-03 · web unit
  // Given a pending payment in the view, When rendered, Then checkPayment is called once for its reference and the view re-read; W-5 shows in a role="status" Notice with a "Check status" button; availability payment_pending shows W-9 with "Check status"
  // Catches: no automatic status check on open, or a pay button while pending (AC-11, AC-13, AC-34, DEC-13)
  test('T-W-2 auto-checks a pending payment and offers Check status', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [opsVan], purchases: { 'ops-1': ['150.50'] } })
      .withBank('timeout_not_received')
      .withStatusCheck('does_not_answer');
    const quote = await fake.createQuote('ops-1');
    await fake.confirmPayment('ops-1', quote.reference);
    fake.calls.length = 0;
    renderWithApi(<CardholderViewPage />, fake, route);

    const pending = await screen.findByRole('region', { name: 'Pending payment' });
    expect(within(pending).getByRole('status')).toHaveTextContent(
      `Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference ${quote.reference}.`,
    );
    expect(within(pending).getByRole('button', { name: 'Check status' })).toBeInTheDocument();
    const balance = screen.getByRole('region', { name: 'Balance' });
    expect(balance).toHaveTextContent(
      `A payment of £150.50 (reference ${quote.reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status.`,
    );
    expect(within(balance).getByRole('button', { name: 'Check status' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Pay full balance' })).toBeNull();
    expect(fake.calls.filter((c) => c === 'getCardholderView' || c === 'checkPayment')).toEqual([
      'getCardholderView',
      'checkPayment',
      'getCardholderView',
    ]);

    await user.click(within(pending).getByRole('button', { name: 'Check status' }));
    await vi.waitFor(() => {
      expect(fake.calls.filter((c) => c === 'checkPayment')).toHaveLength(2);
    });
    expect(await screen.findByRole('region', { name: 'Pending payment' })).toBeInTheDocument();
  });

  // T-W-2 · PR-03 · web unit (AC-34)
  // Given a pending payment whose debit the bank has applied, When the view is opened without pressing Check status, Then it shows the payment completed with balance £0.00 at the bank's time
  // Catches: a stale pending view when the bank has already debited (AC-34, DEC-13)
  test('T-W-2 shows a pending payment as completed when the bank has applied the debit', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-07T10:00:00Z'));
    const fake = new FakeApiClient({
      cards: [opsVan],
      purchases: { 'ops-1': ['150.50'] },
    }).withBank('timeout_debit_applied');
    await fake.confirmPayment('ops-1', (await fake.createQuote('ops-1')).reference);
    vi.setSystemTime(new Date('2026-11-07T10:30:00Z'));
    renderWithApi(<CardholderViewPage />, fake, route);

    const balance = await screen.findByRole('region', { name: 'Balance' });
    await vi.waitFor(() => {
      expect(within(balance).getByText('£0.00')).toBeInTheDocument();
    });
    expect(screen.queryByRole('region', { name: 'Pending payment' })).toBeNull();
    const rows = within(screen.getByRole('table', { name: 'Recent card activity' })).getAllByRole(
      'row',
    );
    expect(within(rows[1] as HTMLElement).getAllByRole('cell')[0]).toHaveTextContent(
      '7 Nov 2026 10:00',
    );
    expect(rows[1]).toHaveTextContent('Completed');
  });

  // T-W-2 · PR-03 · web unit
  // Given getCardholderView rejecting with ApiError 404, When rendered, Then Notice "Card not found"
  // Catches: blank page for unknown cards (design §9)
  test('T-W-2 shows card not found', async () => {
    renderWithApi(<CardholderViewPage />, new FakeApiClient({ cards: [opsVan] }), {
      route: '/cardholder/nope',
      path: '/cardholder/:id',
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Card not found');
    expect(screen.queryByRole('link', { name: 'Pay full balance' })).toBeNull();
  });
});

describe('CardholderShell', () => {
  // T-W-2 · PR-03 · web unit (list addition: the page frame has logic, the cardholder name lookup)
  // Given the cardholder route, When rendered, Then skip link first, header with CardLab, the company, the cardholder's name (not the admin's) and "Cardholder view", main#main
  // Catches: the admin's name shown to the cardholder (DEC-11)
  test('T-W-2 frames the page with the cardholder name, not the admin name', async () => {
    renderWithApi(<CardholderShell />, new FakeApiClient({ cards: [opsVan] }), route);

    const header = screen.getByRole('banner');
    expect(await within(header).findByText('Dana Fox')).toBeInTheDocument();
    expect(within(header).getByText('CardLab')).toBeInTheDocument();
    expect(within(header).getByText('Brightline Supplies Ltd')).toBeInTheDocument();
    expect(within(header).getByText('Cardholder view')).toBeInTheDocument();
    expect(within(header).queryByText('Sam Ellis')).toBeNull();
    expect(names('link')[0]).toBe('Skip to content');
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main');
  });
});
