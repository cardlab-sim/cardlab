import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { FakeApiClient } from '../../api/fakeApiClient';
import { renderWithApi } from '../../test/render';
import { FakeBankPanel } from './FakeBankPanel';

describe('FakeBankPanel', () => {
  // T-W-5 · PR-03 · web unit
  // Given getFakeBank returning complete/answers, When rendered, Then a region named "Fake bank (simulation control)" with labelled selects "Next debit outcome" (five options in R-15 order) and "Next status check", the current setting as text; choosing "Refuse (insufficient funds)" and Set calls setFakeBank({nextDebitOutcome: 'refuse_insufficient_funds', nextStatusCheck: 'answers'}) and shows the new setting
  // Catches: an unlabelled or unusable steering control (R-15, AC-28, CQ-8)
  test('T-W-5 sets the next outcome through labelled controls', async () => {
    const user = userEvent.setup();
    const fake = new FakeApiClient({ cards: [] });
    const setSpy = vi.spyOn(fake, 'setFakeBank');
    const onChanged = vi.fn();
    renderWithApi(<FakeBankPanel onChanged={onChanged} />, fake);

    const region = await screen.findByRole('region', { name: 'Fake bank (simulation control)' });
    expect(region).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Fake bank (simulation control)' })).toBeVisible();
    const outcome = screen.getByRole('combobox', { name: 'Next debit outcome' });
    expect(Array.from((outcome as HTMLSelectElement).options).map((o) => o.text)).toEqual([
      'Complete',
      'Refuse (insufficient funds)',
      'Refuse (account unavailable)',
      'Time out, debit applied',
      'Time out, debit not received',
    ]);
    expect(screen.getByRole('combobox', { name: 'Next status check' })).toHaveValue('answers');
    expect(region).toHaveTextContent('Current setting: next debit outcome Complete');

    await user.selectOptions(outcome, 'Refuse (insufficient funds)');
    await user.click(screen.getByRole('button', { name: 'Set' }));

    expect(setSpy).toHaveBeenCalledExactlyOnceWith({
      nextDebitOutcome: 'refuse_insufficient_funds',
      nextStatusCheck: 'answers',
    });
    expect(
      await screen.findByText(/Current setting: next debit outcome Refuse \(insufficient funds\)/),
    ).toBeInTheDocument();
    expect((await fake.getFakeBank()).nextDebitOutcome).toBe('refuse_insufficient_funds');
    expect(onChanged).toHaveBeenCalledTimes(1);
  });
});
