import { screen, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { FakeApiClient } from '../api/fakeApiClient';
import type { CardDetail, PaymentRecord } from '../api/types';
import { renderWithApi } from '../test/render';
import { PaymentRecordPage } from './PaymentRecordPage';

const card: CardDetail = {
  id: 'ops-1',
  name: 'Ops van',
  cardholderName: 'Priya Nair',
  lastFour: '0001',
  status: 'active',
  limits: [],
  balanceOwed: '0.00',
};

const SIX_LINES = [
  'Amount: £150.50',
  'Fee: £0.00 (no fee)',
  'Total to pay: £150.50',
  'When: normally within seconds of confirming.',
  'From: Brightline Supplies Ltd current account, ending 7731',
  'To: Ops van card, ending 0001',
];

/** A completed, steered (simulated) record with hand-written literals. */
function completedRecord(): PaymentRecord {
  return {
    quote: {
      reference: 'PAY-00000001',
      kind: 'full',
      amount: '150.50',
      fee: '0.00',
      total: '150.50',
      from: 'Brightline Supplies Ltd current account, ending 7731',
      to: 'Ops van card, ending 0001',
      wording: {
        version: 'v1',
        heading: 'Pay your full balance',
        lines: SIX_LINES,
        button: 'Confirm payment of £150.50',
      },
      createdAt: '2026-10-24T23:29:00Z',
    },
    status: 'confirmed',
    refusalReason: null,
    balanceAtRefusal: null,
    acceptance: {
      at: '2026-10-24T23:30:00Z',
      actor: { kind: 'cardholder', name: 'Priya Nair' },
      authorityBasis: 'Standing authority from Brightline Supplies Ltd',
      origin: 'cardholder_view',
    },
    payment: {
      state: 'completed',
      amount: '150.50',
      fee: '0.00',
      createdAt: '2026-10-24T23:30:00Z',
      settledAt: '2026-10-24T23:30:02Z',
      effectiveAt: '2026-10-24T23:30:01Z',
      bankDebitedAt: '2026-10-24T23:30:01Z',
      failureReason: null,
      balanceBefore: '150.50',
      balanceAfter: '0.00',
      simulated: true,
    },
    events: [
      {
        at: '2026-10-24T23:30:00Z',
        kind: 'confirm_created',
        from: null,
        to: 'pending',
        detail: null,
        simulated: false,
      },
      {
        at: '2026-10-24T23:30:02Z',
        kind: 'bank_completed',
        from: 'pending',
        to: 'completed',
        detail: 'ledger entry written',
        simulated: true,
      },
    ],
  };
}

class RecordFake extends FakeApiClient {
  constructor(private readonly record: PaymentRecord) {
    super({ cards: [card] });
  }

  override getPaymentRecord(): Promise<PaymentRecord> {
    return Promise.resolve(structuredClone(this.record));
  }
}

function renderRecord(fake: FakeApiClient, reference = 'PAY-00000001') {
  return renderWithApi(<PaymentRecordPage />, fake, {
    route: `/cards/ops-1/payments/${reference}`,
    path: '/cards/:id/payments/:reference',
  });
}

function section(name: string) {
  return within(screen.getByRole('region', { name }));
}

describe('PaymentRecordPage', () => {
  // T-W-6 · PR-07 · web unit
  // Given a completed simulated record, When rendered at /cards/:id/payments/:reference, Then "What the cardholder saw" lists the wording heading, the six lines verbatim, the button text and "Wording version v1"; Acceptance shows time (London), "Priya Nair" (cardholder), authority basis and origin; Payment shows a Completed badge, £150.50, fee £0.00, balance before/after, effective time and "Simulated outcome"; the events table lists each event with time, kind, from → to and detail
  // Catches: an admin record missing any AC-20 element (AC-20, R-14)
  test('T-W-6 shows what was shown, accepted and the outcome', async () => {
    renderRecord(new RecordFake(completedRecord()));

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('PAY-00000001');

    const shown = section('What the cardholder saw');
    expect(shown.getByText('Pay your full balance')).toBeInTheDocument();
    const lines = shown.getAllByRole('listitem').map((item) => item.textContent);
    expect(lines).toEqual(SIX_LINES);
    expect(shown.getByText('Confirm payment of £150.50')).toBeInTheDocument();
    expect(shown.getByText('Wording version v1')).toBeInTheDocument();
    expect(shown.getByText('Amount').nextElementSibling).toHaveTextContent('£150.50');
    expect(shown.getByText('Fee').nextElementSibling).toHaveTextContent('£0.00');
    expect(shown.getByText('Total').nextElementSibling).toHaveTextContent('£150.50');
    expect(shown.getByText('From').nextElementSibling).toHaveTextContent(
      'Brightline Supplies Ltd current account, ending 7731',
    );
    expect(shown.getByText('To').nextElementSibling).toHaveTextContent('Ops van card, ending 0001');

    const accepted = section('Acceptance');
    expect(accepted.getByText('25 Oct 2026 00:30')).toBeInTheDocument();
    expect(accepted.getByText('Priya Nair (cardholder)')).toBeInTheDocument();
    expect(
      accepted.getByText('Standing authority from Brightline Supplies Ltd'),
    ).toBeInTheDocument();
    expect(accepted.getByText('Cardholder view')).toBeInTheDocument();

    const payment = section('Payment');
    expect(payment.getByText('Completed')).toBeInTheDocument();
    expect(payment.getByText('Fee').nextElementSibling).toHaveTextContent('£0.00');
    expect(payment.getByText('Balance owed before').nextElementSibling).toHaveTextContent(
      '£150.50',
    );
    expect(payment.getByText('Balance owed after').nextElementSibling).toHaveTextContent('£0.00');
    expect(payment.getByText('Effective time').nextElementSibling).toHaveTextContent(
      '25 Oct 2026 00:30',
    );
    expect(payment.getByText('Settled at').nextElementSibling).toHaveTextContent(
      '25 Oct 2026 00:30',
    );
    expect(payment.getByText('Bank debit time').nextElementSibling).toHaveTextContent(
      '25 Oct 2026 00:30',
    );
    expect(payment.getByText('Simulated outcome')).toBeInTheDocument();

    const events = within(screen.getByRole('table', { name: 'Events' }));
    const rows = events.getAllByRole('row').slice(1);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('confirm_created');
    expect(rows[0]).toHaveTextContent('— → Pending');
    expect(rows[1]).toHaveTextContent('bank_completed');
    expect(rows[1]).toHaveTextContent('Pending → Completed');
    expect(rows[1]).toHaveTextContent('ledger entry written');
    expect(rows[1]).toHaveTextContent('Yes');
    expect(rows[0]).not.toHaveTextContent('Yes');
  });

  // T-W-6 · PR-07 · web unit
  // Given a refused reference / a pending payment / a smoke-origin record / a 404, When rendered, Then refusal reason and balance at refusal / a Pending badge and no balances / an "origin: smoke" badge / Notice "Payment not found" with a link back to the card
  // Catches: states other than completed rendered wrongly (AC-20, DEC-15)
  test('T-W-6 renders a refused reference with its reason and balance', async () => {
    const record: PaymentRecord = {
      ...completedRecord(),
      status: 'refused',
      refusalReason: 'balance_changed',
      balanceAtRefusal: '301.00',
      payment: null,
      acceptance: { ...completedRecord().acceptance, at: '2026-10-24T23:31:00Z' },
    };
    renderRecord(new RecordFake(record));

    await screen.findByRole('heading', { level: 1 });
    const refusal = section('Refusal');
    expect(refusal.getByText('balance_changed')).toBeInTheDocument();
    expect(refusal.getByText('£301.00')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Payment' })).toBeNull();
    expect(section('Acceptance').getByText('25 Oct 2026 00:31')).toBeInTheDocument();
  });

  test('T-W-6 renders a pending payment without balances', async () => {
    const base = completedRecord();
    const record: PaymentRecord = {
      ...base,
      payment: {
        ...base.payment!,
        state: 'pending',
        settledAt: null,
        effectiveAt: null,
        bankDebitedAt: null,
        balanceBefore: null,
        balanceAfter: null,
        simulated: false,
      },
    };
    renderRecord(new RecordFake(record));

    await screen.findByRole('heading', { level: 1 });
    const payment = section('Payment');
    expect(payment.getByText('Pending')).toBeInTheDocument();
    expect(payment.queryByText('Balance owed before')).toBeNull();
    expect(payment.queryByText('Balance owed after')).toBeNull();
    expect(payment.queryByText('Simulated outcome')).toBeNull();
    expect(payment.queryByText('Effective time')).toBeNull();
  });

  test('T-W-6 shows the smoke origin badge', async () => {
    const base = completedRecord();
    const record: PaymentRecord = {
      ...base,
      acceptance: { ...base.acceptance, origin: 'deploy_smoke' },
    };
    renderRecord(new RecordFake(record));

    await screen.findByRole('heading', { level: 1 });
    expect(section('Acceptance').getByText('origin: smoke')).toBeInTheDocument();
  });

  test('T-W-6 shows a not-found notice with a link back to the card', async () => {
    renderRecord(new FakeApiClient({ cards: [card] }), 'PAY-NOPE0000');

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent('Payment not found');
    expect(within(notice).getByRole('link', { name: 'Back to the card' })).toHaveAttribute(
      'href',
      '/cards/ops-1',
    );
  });
});
