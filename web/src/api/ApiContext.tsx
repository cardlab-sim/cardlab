import { createContext, useContext, type ReactNode } from 'react';
import type { ApiClient } from './apiClient';

const ApiContext = createContext<ApiClient | null>(null);

/**
 * Provides the ApiClient to the component tree (HttpApiClient in main.tsx, FakeApiClient in tests).
 * Complete in PR-00.
 */
export function ApiProvider({ client, children }: { client: ApiClient; children: ReactNode }) {
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>;
}

/**
 * The current ApiClient.
 * @throws Error when used outside an ApiProvider.
 */
export function useApi(): ApiClient {
  const client = useContext(ApiContext);
  if (client === null) {
    throw new Error('useApi must be used inside <ApiProvider>');
  }
  return client;
}
