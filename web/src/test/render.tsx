import { render, type RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { ApiProvider } from '../api/ApiContext';
import type { ApiClient } from '../api/apiClient';

/** Options for renderWithApi. */
export interface RenderWithApiOptions {
  /** Initial URL, e.g. '/cards/abc'. Default '/'. */
  route?: string;
  /** Route pattern the element is mounted on, e.g. '/cards/:id'. Default: render `ui` directly. */
  path?: string;
}

/**
 * Renders `ui` inside an ApiProvider with `client` and a MemoryRouter (design §15.4 "New web unit test").
 * @returns Testing Library's render result.
 */
export function renderWithApi(
  ui: ReactElement,
  client: ApiClient,
  options: RenderWithApiOptions = {},
): RenderResult {
  const { route = '/', path } = options;
  return render(
    <ApiProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        {path === undefined ? (
          ui
        ) : (
          <Routes>
            <Route path={path} element={ui} />
          </Routes>
        )}
      </MemoryRouter>
    </ApiProvider>,
  );
}
