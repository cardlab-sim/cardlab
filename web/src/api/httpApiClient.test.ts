import { describe, test } from 'vitest';

describe('HttpApiClient', () => {
  // T-W-5 · PR-02 · web unit (fetch stubbed with vi.stubGlobal)
  // Given a stubbed fetch, When each method is called, Then the §7 method and path (ids URI-encoded), JSON body for setLimit {amount} and simulatePurchase, and the parsed JSON result; removeLimit resolves on 204
  // Catches: wrong verbs/paths silently breaking the UI against the real API
  test.todo('T-W-5 calls the documented method and path for each endpoint');

  // T-W-5 · PR-02 · web unit
  // Given 404 ProblemDetails on getCard and 400 with errors.amount on setLimit, When called, Then rejects with ApiError(status, problem); simulatePurchase resolves with the decision body on 200, 400 and 404 and rejects with ApiError on 503
  // Catches: inline errors impossible because ApiError lost the ProblemDetails (AC-9, AC-22)
  test.todo('T-W-5 maps error statuses to ApiError but returns decisions for 400/404 on simulate');
});
