import type { RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import type { ApiClient } from '../api/apiClient';
import { notImplemented } from '../notImplemented';

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
 * Implemented in PR-02.
 */
export function renderWithApi(
  ui: ReactElement,
  client: ApiClient,
  options?: RenderWithApiOptions,
): RenderResult {
  return notImplemented('PR-02', ui, client, options);
}
