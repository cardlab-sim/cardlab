import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StrictMode } from 'react';
import { Route, Routes } from 'react-router';
import { describe, expect, test } from 'vitest';
import { FakeApiClient } from '../../api/fakeApiClient';
import type { CardDetail } from '../../api/types';
import { CardholderShell } from '../../components/CardholderShell';
import { renderWithApi } from '../../test/render';
import { ReviewPage } from './ReviewPage';

const opsVan: CardDetail = {
  id: 'ops-1',
  name: 'Ops van',
  cardholderName: 'Dana Fox',
  lastFour: '0001',
  status: 'active',
  limits: [],
  balanceOwed: '0.00',
};

async function rejection(promise: Promise<unknown>): Promise<unknown> {
  return promise.then(
    () => undefined,
    (e: unknown) => e,
  );
}

const owing = (...purchases: string[]) =>
  new FakeApiClient({ cards: [opsVan], purchases: { 'ops-1': purchases } });

/** The review page inside the cardholder frame, as the app mounts it, StrictMode on like main.tsx. */
function renderReview(fake: FakeApiClient) {
  return renderWithApi(
    <StrictMode>
      <Routes>
        <Route element={<CardholderShell />}>
          <Route path="pay" element={<ReviewPage />} />
          <Route path="payments/:reference" element={<p>Receipt page</p>} />
          <Route index element={<p>Card view page</p>} />
        </Route>
      </Routes>
    </StrictMode>,
    fake,
    { route: '/cardholder/ops-1/pay', path: '/cardholder/:id/*' },
  );
}

describe('ReviewPage', () => {
  // T-W-3 · PR-03 · web unit
  // Given createQuote returning the W-1 wording for £150.50, When rendered at /cardholder/:id/pay, Then exactly one quote is created (also under StrictMode), the heading and the six lines appear in order inside one main region with the button "Confirm payment of £150.50", and there is no fake-bank control
  // Catches: two references per visit, or fee/execution time not on the same screen as the button (AC-6, AC-33, OB-7)
  test('T-W-3 renders W-1 and the confirm button on one screen', async () => {
    const fake = owing('150.50');
    renderReview(fake);

    const main = screen.getByRole('main');
    const button = await within(main).findByRole('button', {
      name: 'Confirm payment of £150.50',
    });
    expect(fake.calls.filter((c) => c === 'createQuote')).toHaveLength(1);
    expect(within(main).getByRole('heading', { level: 1 })).toHaveTextContent(
      'Pay your full balance',
    );
    const rows = Array.from(main.querySelectorAll('dl > div')).map((row) => row.textContent);
    expect(rows).toEqual([
      'Amount: £150.50',
      'Fee: £0.00 (no fee)',
      'Total to pay: £150.50',
      'When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.',
      'From: Brightline Supplies Ltd current account, ending 7731',
      'To: Ops van card, ending 0001',
    ]);
    const heading = within(main).getByRole('heading', { level: 1 });
    const list = main.querySelector('dl') as HTMLElement;
    expect(heading.compareDocumentPosition(list) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(list.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(main).getByRole('link', { name: 'Back' })).toHaveAttribute(
      'href',
      '/cardholder/ops-1',
    );
    expect(screen.queryByRole('region', { name: /Fake bank/ })).toBeNull();
    expect(screen.queryByText(/Fake bank/)).toBeNull();
    expect(fake.calls).not.toContain('getFakeBank');
  });

  // T-W-3 · PR-03 · web unit
  // Given confirmPayment held by FakeApiClient.hold(), When the button is clicked twice and Enter is pressed, Then confirmPayment was called once, the button shows a pending state with aria-disabled="true" and keeps focus; on resolve the app navigates to /cardholder/:id/payments/:reference
  // Catches: double submit or focus loss while confirming (AC-9, 001 BUG-010)
  test('T-W-3 guards the confirm button while pending', async () => {
    const user = userEvent.setup();
    const fake = owing('150.50');
    renderReview(fake);
    const button = await screen.findByRole('button', { name: 'Confirm payment of £150.50' });

    const release = fake.hold();
    await user.click(button);
    await user.click(button);
    await user.keyboard('{Enter}');

    expect(fake.calls.filter((c) => c === 'confirmPayment')).toHaveLength(1);
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).toHaveFocus();

    release();
    expect(await screen.findByText('Receipt page')).toBeInTheDocument();
  });

  // T-W-3 · PR-03 · web unit
  // Given confirmPayment rejecting with 409 balance_changed (message W-6), When confirm, Then the W-6 message verbatim in a role="alert" Notice and a "Review again" button that creates a new quote; given createQuote rejecting 409 nothing_to_pay, Then its message and a Back link
  // Catches: refusals shown without the next step, or client-made wording (AC-8, AC-30, DEC-10)
  test('T-W-3 shows refusals verbatim with the next-step control', async () => {
    const user = userEvent.setup();
    const fake = owing('150.50');
    renderReview(fake);
    const button = await screen.findByRole('button', { name: 'Confirm payment of £150.50' });
    fake.addPurchase('ops-1', '20.00');

    await user.click(button);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.',
    );
    expect(screen.queryByRole('button', { name: /Confirm payment/ })).toBeNull();
    expect(document.activeElement).not.toBe(document.body);
    await user.click(screen.getByRole('button', { name: 'Review again' }));
    expect(
      await screen.findByRole('button', { name: 'Confirm payment of £170.50' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(fake.calls.filter((c) => c === 'createQuote')).toHaveLength(2);
  });

  // T-W-3 · PR-03 · web unit (AC-30)
  // Given the page's reference already refused (balance changed), When confirm is pressed again, Then 409 quote_refused shows W-6 in an alert with "Review again", focus moved into the notice
  // Catches: the retried-confirm refusal falling into the generic error (AC-30)
  test('T-W-3 shows the repeated-confirm refusal of a refused reference with Review again', async () => {
    const user = userEvent.setup();
    const fake = owing('150.50');
    renderReview(fake);
    const button = await screen.findByRole('button', { name: 'Confirm payment of £150.50' });
    fake.addPurchase('ops-1', '20.00');
    await rejection(fake.confirmPayment('ops-1', 'PAY-00000001'));

    await user.click(button);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(
      'Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now £170.50. Review the new amount and confirm again if you want to pay it.',
    );
    expect(screen.getByRole('button', { name: 'Review again' })).toBeInTheDocument();
    expect(alert.parentElement).toHaveFocus();
  });

  test('T-W-3 shows a quote refusal with a Back link and no confirm button', async () => {
    renderReview(owing());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Nothing to pay. Your card balance is £0.00.',
    );
    expect(screen.getByRole('link', { name: 'Back' })).toHaveAttribute('href', '/cardholder/ops-1');
    expect(screen.queryByRole('button', { name: /Confirm payment/ })).toBeNull();
  });

  test('T-W-3 sends a pending refusal on confirm to the card view with W-9', async () => {
    const user = userEvent.setup();
    const fake = owing('150.50').withBank('timeout_not_received');
    renderReview(fake);
    const button = await screen.findByRole('button', { name: 'Confirm payment of £150.50' });
    // Another visit's payment becomes pending before this one is confirmed.
    const second = await fake.createQuote('ops-1');
    await fake.confirmPayment('ops-1', second.reference);

    await user.click(button);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      `A payment of £150.50 (reference ${second.reference}) is still pending.`,
    );
    expect(screen.getByRole('link', { name: 'Back to your card' })).toHaveAttribute(
      'href',
      '/cardholder/ops-1',
    );
  });
});
