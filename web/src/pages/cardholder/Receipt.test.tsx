import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Navigate, Route, Routes } from 'react-router';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { FakeApiClient } from '../../api/fakeApiClient';
import type { CardDetail, Receipt } from '../../api/types';
import { renderWithApi } from '../../test/render';
import { ReceiptPage } from './ReceiptPage';

const opsVan: CardDetail = {
  id: 'ops-1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [],
  balanceOwed: '0.00',
};

const FROM = 'Brightline Supplies Ltd current account, ending 7731';
const TO = 'Ops van card, ending 0001';

afterEach(() => {
  vi.useRealTimers();
});

function owing(): FakeApiClient {
  return new FakeApiClient({ cards: [opsVan], purchases: { 'ops-1': ['150.50'] } });
}

/** Opens the receipt page for a reference that exists on the fake. */
async function openReceipt(fake: FakeApiClient, settle?: 'check') {
  const quote = await fake.createQuote('ops-1');
  await fake.confirmPayment('ops-1', quote.reference);
  if (settle === 'check') {
    await fake.checkPayment('ops-1', quote.reference);
  }
  fake.calls.length = 0;
  renderWithApi(<ReceiptPage />, fake, {
    route: `/cardholder/ops-1/payments/${quote.reference}`,
    path: '/cardholder/:id/payments/:reference',
  });
  return quote.reference;
}

function fields(): Record<string, string | null> {
  const terms = Array.from(document.querySelectorAll('dl dt'));
  return Object.fromEntries(
    terms.map((dt) => [dt.textContent ?? '', dt.nextElementSibling?.textContent ?? null]),
  );
}

describe('ReceiptPage', () => {
  // T-W-4 · PR-03 · web unit
  // Given a completed / failed (insufficient_funds) / failed (not_received) / pending receipt, When rendered, Then heading "Payment complete" / "Payment not made" / "Payment not made" / "Payment pending", the message verbatim (role="status" for completed and pending, role="alert" for failed), Reference, Amount, Fee £0.00, Time in Europe/London, From, To, an outcome text badge (plus "Not received" for not_received), and no fake-bank control
  // Catches: outcome shown by colour only, a missing fee, or the control on the receipt (AC-7, AC-12, AC-13, AC-33, AC-35, R-12)
  test.each([
    {
      name: 'completed',
      bank: 'complete' as const,
      settle: undefined,
      heading: 'Payment complete',
      role: 'status',
      outcome: 'Completed',
      message: (ref: string) =>
        `Payment complete. £150.50 has left your current account and paid your card. Your card balance is now £0.00. Reference ${ref}.`,
    },
    {
      name: 'failed (insufficient funds)',
      bank: 'refuse_insufficient_funds' as const,
      settle: undefined,
      heading: 'Payment not made',
      role: 'alert',
      outcome: 'Failed',
      message: (ref: string) =>
        `Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still £150.50. Add money to the account, then try again. Reference ${ref}.`,
    },
    {
      name: 'failed (not received)',
      bank: 'timeout_not_received' as const,
      settle: 'check' as const,
      heading: 'Payment not made',
      role: 'alert',
      outcome: 'Failed',
      message: (ref: string) =>
        `Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still £150.50. Try again later. Reference ${ref}.`,
    },
    {
      name: 'pending',
      bank: 'timeout_not_received' as const,
      settle: undefined,
      heading: 'Payment pending',
      role: 'status',
      outcome: 'Pending',
      message: (ref: string) =>
        `Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows £150.50 until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference ${ref}.`,
    },
  ])('T-W-4 renders $name with its message and fields', async (c) => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-07T10:00:00Z'));
    const fake = owing().withBank(c.bank);
    const reference = await openReceipt(fake, c.settle);

    expect(await screen.findByRole('heading', { level: 1, name: c.heading })).toBeInTheDocument();
    expect(screen.getByRole(c.role)).toHaveTextContent(c.message(reference));
    expect(fields()).toEqual({
      Reference: reference,
      Amount: '£150.50',
      Fee: '£0.00',
      Time: '7 Nov 2026 10:00',
      From: FROM,
      To: TO,
      Outcome: c.name.includes('not received') ? `${c.outcome}Not received` : c.outcome,
    });
    expect(screen.getByRole('link', { name: 'Back to your card' })).toHaveAttribute(
      'href',
      '/cardholder/ops-1',
    );
    expect(screen.queryByText(/Fake bank/)).toBeNull();
    expect(fake.calls).not.toContain('getFakeBank');
    const check = screen.queryByRole('button', { name: 'Check status' });
    expect(check === null).toBe(c.name !== 'pending');
    const review = screen.queryByRole('link', { name: 'Review again' });
    expect(review === null).toBe(!c.name.startsWith('failed'));
  });

  // T-W-4 · PR-03 · web unit
  // Given a pending receipt and checkPayment returning the W-10 message, When "Check status" is clicked, Then the new message replaces the old and the button stays available
  // Catches: no way to re-check an unanswered payment (AC-16)
  test('T-W-4 checks status again from the receipt', async () => {
    const user = userEvent.setup();
    const fake = owing().withBank('timeout_not_received').withStatusCheck('does_not_answer');
    const reference = await openReceipt(fake);
    const button = await screen.findByRole('button', { name: 'Check status' });

    await user.click(button);

    const w10 = `Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference ${reference}.`;
    expect(await screen.findByText(w10)).toBeInTheDocument();
    expect(screen.queryByText(/^Payment pending\. Your bank has not confirmed/)).toBeNull();
    expect(screen.getByRole('status')).toHaveTextContent(w10);
    expect(screen.getByRole('button', { name: 'Check status' })).toBeInTheDocument();
    expect(fake.calls.filter((c) => c === 'checkPayment')).toHaveLength(1);

    // The steering is one-shot: arm it again for the second check.
    fake.withStatusCheck('does_not_answer');
    await user.click(screen.getByRole('button', { name: 'Check status' }));
    await vi.waitFor(() => {
      expect(fake.calls.filter((c) => c === 'checkPayment')).toHaveLength(2);
    });
  });

  test('T-W-4 turns a pending receipt into its final outcome when the bank answers the check', async () => {
    const user = userEvent.setup();
    const fake = owing().withBank('timeout_not_received');
    await openReceipt(fake);

    await user.click(await screen.findByRole('button', { name: 'Check status' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Payment not made' }),
    ).toBeInTheDocument();
    const alert = screen.getByRole('alert');
    expect(alert.parentElement).toHaveFocus();
    expect(screen.queryByRole('button', { name: 'Check status' })).toBeNull();
  });

  // T-W-4 · PR-03 · web unit
  // Given no navigation state, When rendered, Then getReceipt(id, reference) is loaded; a 404 shows "This payment reference was not found"
  // Catches: receipt page broken on reload (design §7, §9)
  test('T-W-4 reloads the receipt from the API', async () => {
    const fake = owing();
    const reference = await openReceipt(fake);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Payment complete' }),
    ).toBeVisible();
    expect(fake.calls).toEqual(['getReceipt']);
    expect(fields()['Reference']).toBe(reference);
  });

  test('T-W-4 shows not found for an unknown reference', async () => {
    renderWithApi(<ReceiptPage />, owing(), {
      route: '/cardholder/ops-1/payments/PAY-99999999',
      path: '/cardholder/:id/payments/:reference',
    });

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This payment reference was not found',
    );
    expect(screen.queryByRole('heading', { level: 1, name: /Payment/ })).toBeNull();
  });

  test('T-W-4 paints the navigation state first and keeps it when the re-read fails', async () => {
    const fake = owing();
    renderWithApi(
      <Routes>
        <Route
          path="/start"
          element={
            <Navigate to="/cardholder/ops-1/payments/PAY-STATE" state={stateReceipt()} replace />
          }
        />
        <Route path="/cardholder/:id/payments/:reference" element={<ReceiptPage />} />
      </Routes>,
      fake,
      { route: '/start' },
    );

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Payment complete. From the navigation state.',
    );
    await vi.waitFor(() => {
      expect(fake.calls).toEqual(['getReceipt']);
    });
    expect(screen.getByRole('status')).toHaveTextContent('From the navigation state.');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  // T-W-4 · PR-03 · web unit (M3: history state survives reload and back/forward)
  // Given a pending receipt in history state while the bank has since applied the debit, When the page opens, Then it re-reads and shows "Payment complete"
  // Catches: a stale pending receipt after reload or back navigation (R-12)
  test('T-W-4 replaces a stale navigation-state receipt with the current one', async () => {
    const fake = owing().withBank('timeout_debit_applied');
    const quote = await fake.createQuote('ops-1');
    const stale = await fake.confirmPayment('ops-1', quote.reference);
    await fake.checkPayment('ops-1', quote.reference);
    expect(stale.state).toBe('pending');
    renderWithApi(
      <Routes>
        <Route
          path="/start"
          element={
            <Navigate to={`/cardholder/ops-1/payments/${quote.reference}`} state={stale} replace />
          }
        />
        <Route path="/cardholder/:id/payments/:reference" element={<ReceiptPage />} />
      </Routes>,
      fake,
      { route: '/start' },
    );

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Payment complete' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Check status' })).toBeNull();
  });
});

function stateReceipt(): Receipt {
  return {
    reference: 'PAY-STATE',
    state: 'completed',
    amount: '150.50',
    fee: '0.00',
    time: '2026-11-07T10:00:00Z',
    from: FROM,
    to: TO,
    failureReason: null,
    message: 'Payment complete. From the navigation state.',
    balanceOwed: '0.00',
    repeated: false,
    origin: 'cardholder_view',
  };
}
