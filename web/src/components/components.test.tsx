import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, test, vi } from 'vitest';
import { AppShell } from './AppShell';
import { Badge } from './Badge';
import { Button } from './Button';
import { DataTable } from './DataTable';
import { SelectField, TextField } from './Field';
import { Notice } from './Notice';

describe('primitives', () => {
  // T-W-6 · PR-02 · web unit
  // Given TextField with an error, When rendered, Then the input is labelled by its <label>, has aria-invalid and aria-describedby pointing to an element with role="alert" holding the error; without error none of these
  // Catches: errors not announced or not tied to the field (§4.1, AC-37)
  test('T-W-6 fields have visible labels and announced inline errors', () => {
    const { rerender } = render(<TextField id="amount" label="Amount" error="Enter an amount" />);
    const input = screen.getByLabelText('Amount');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Enter an amount');
    expect(input).toHaveAttribute('aria-describedby', alert.id);

    rerender(<TextField id="amount" label="Amount" />);
    expect(screen.getByLabelText('Amount')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('Amount')).not.toHaveAttribute('aria-describedby');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  test('T-W-6 select field is labelled and announces its error', () => {
    render(
      <SelectField
        id="category"
        label="Category"
        value="fuel"
        options={[
          { value: 'travel', label: 'Travel' },
          { value: 'fuel', label: 'Fuel' },
        ]}
        onChange={() => {}}
        error="Pick one"
      />,
    );
    const select = screen.getByLabelText('Category');
    expect(select).toHaveValue('fuel');
    expect(select).toHaveAttribute('aria-invalid', 'true');
    expect(select).toHaveAttribute('aria-describedby', screen.getByRole('alert').id);
    expect(screen.getByRole('alert')).toHaveTextContent('Pick one');
  });

  // T-W-6 · PR-02 · web unit
  // Given Button pending, When rendered, Then aria-disabled with aria-busy and the pending label; Badge renders its text; Notice error has role="alert", success role="status"; DataTable renders a caption, th scope=col and the empty text
  // Catches: colour-only status, toast-only feedback, inaccessible tables (§4.1)
  test('T-W-6 button, badge, notice and table are accessible', () => {
    const { rerender } = render(
      <Button pending pendingLabel="Saving…">
        Save
      </Button>,
    );
    const pending = screen.getByRole('button', { name: 'Saving…' });
    expect(pending).toHaveAttribute('aria-disabled', 'true');
    expect(pending).toHaveAttribute('aria-busy', 'true');

    rerender(<Button>Save</Button>);
    const idle = screen.getByRole('button', { name: 'Save' });
    expect(idle).toBeEnabled();
    expect(idle).toHaveAttribute('type', 'button');
    expect(idle).not.toHaveAttribute('aria-busy', 'true');

    rerender(<Badge tone="frozen">Frozen</Badge>);
    expect(screen.getByText('Frozen')).toBeInTheDocument();

    rerender(
      <>
        <Notice tone="error">Broke</Notice>
        <Notice tone="success" title="Saved">
          Limit saved
        </Notice>
      </>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Broke');
    expect(screen.getByRole('status')).toHaveTextContent('Saved');
    expect(screen.getByRole('status')).toHaveTextContent('Limit saved');

    rerender(
      <DataTable
        caption="Cards"
        columns={[{ header: 'Name', cell: (r: { n: string }) => r.n }]}
        rows={[]}
        rowKey={(r) => r.n}
        emptyText="Nothing here"
      />,
    );
    const table = screen.getByRole('table', { name: 'Cards' });
    expect(within(table).getByRole('columnheader', { name: 'Name' })).toHaveAttribute(
      'scope',
      'col',
    );
    expect(within(table).getByText('Nothing here')).toBeInTheDocument();

    rerender(
      <DataTable
        caption="Cards"
        columns={[{ header: 'Name', cell: (r: { n: string }) => r.n }]}
        rows={[{ n: 'Ops van' }]}
        rowKey={(r) => r.n}
        emptyText="Nothing here"
      />,
    );
    expect(screen.getByRole('cell', { name: 'Ops van' })).toBeInTheDocument();
    expect(screen.queryByText('Nothing here')).toBeNull();
  });

  // BUG-007: a horizontally scrollable table container must be keyboard reachable and named (axe scrollable-region-focusable)
  test('DataTable scroll container is a focusable region named by the caption', () => {
    render(
      <DataTable
        caption="Cards"
        columns={[{ header: 'Name', cell: (r: { n: string }) => r.n }]}
        rows={[{ n: 'Ops van' }]}
        rowKey={(r) => r.n}
        emptyText="Nothing here"
      />,
    );
    const region = screen.getByRole('region', { name: 'Cards' });
    expect(region).toHaveAttribute('tabindex', '0');
    expect(within(region).getByRole('table', { name: 'Cards' })).toBeInTheDocument();
  });

  test('T-W-6 app shell has header identity, a skip link first, and main content', () => {
    render(
      <MemoryRouter>
        <AppShell>
          <p>Body</p>
        </AppShell>
      </MemoryRouter>,
    );
    const links = screen.getAllByRole('link');
    expect(links[0]).toHaveTextContent('Skip to content');
    expect(links[0]).toHaveAttribute('href', '#main');
    expect(screen.getByRole('link', { name: 'CardLab' })).toHaveAttribute('href', '/');
    const header = screen.getByRole('banner');
    expect(header).toHaveTextContent('Brightline Supplies Ltd');
    expect(header).toHaveTextContent('Sam Ellis');
    expect(within(screen.getByRole('main')).getByText('Body')).toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main');
  });
});

// T-W-6b · PR-13 (BUG-010) · web unit
// Given a pending Button, When it is focused and activated, Then it keeps focus, is not natively disabled and ignores the click
// Catches: focus falling to the page body after an action (WCAG 2.4.3), double submits
test('T-W-6b a pending button keeps focus and ignores clicks', async () => {
  const onClick = vi.fn();
  render(
    <Button pending pendingLabel="Saving…" onClick={onClick}>
      Save
    </Button>,
  );
  const button = screen.getByRole('button', { name: 'Saving…' });
  button.focus();
  await userEvent.click(button);
  expect(button).toHaveFocus();
  expect(button).toBeEnabled();
  expect(onClick).not.toHaveBeenCalled();
});
