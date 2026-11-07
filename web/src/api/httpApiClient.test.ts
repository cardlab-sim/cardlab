import { afterEach, describe, expect, test, vi } from 'vitest';
import { ApiError } from './apiClient';
import { HttpApiClient } from './httpApiClient';
import type { Decision } from './types';

function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
}

function stubFetch(response: Response) {
  const fetchMock = vi.fn(() => Promise.resolve(response));
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function lastCall(fetchMock: ReturnType<typeof stubFetch>): { url: string; init: RequestInit } {
  const call = fetchMock.mock.calls.at(-1) as unknown as [string, RequestInit | undefined];
  return { url: call[0], init: call[1] ?? {} };
}

const decision = (outcome: 'approved' | 'declined', reason: Decision['reason']): Decision => ({
  outcome,
  reason,
  transactionId: null,
  limits: [],
  problems: [],
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('HttpApiClient', () => {
  // T-W-5 · PR-02 · web unit (fetch stubbed with vi.stubGlobal)
  // Given a stubbed fetch, When each method is called, Then the §7 method and path (ids URI-encoded), JSON body for setLimit {amount} and simulatePurchase, and the parsed JSON result; removeLimit resolves on 204
  // Catches: wrong verbs/paths silently breaking the UI against the real API
  test('T-W-5 calls the documented method and path for each endpoint', async () => {
    const client = new HttpApiClient();
    const cards = [{ id: 'a', name: 'Ops van' }];

    let fetchMock = stubFetch(jsonResponse(200, cards));
    expect(await client.listCards()).toEqual(cards);
    expect(lastCall(fetchMock).url).toBe('/api/cards');
    expect(lastCall(fetchMock).init.method ?? 'GET').toBe('GET');

    fetchMock = stubFetch(jsonResponse(200, { id: 'a/b', limits: [] }));
    expect(await client.getCard('a/b')).toEqual({ id: 'a/b', limits: [] });
    expect(lastCall(fetchMock).url).toBe('/api/cards/a%2Fb');

    fetchMock = stubFetch(jsonResponse(200, { status: 'frozen' }));
    expect(await client.freeze('c1')).toEqual({ status: 'frozen' });
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/freeze');
    expect(lastCall(fetchMock).init.method).toBe('POST');

    fetchMock = stubFetch(jsonResponse(200, { status: 'active' }));
    expect(await client.unfreeze('c1')).toEqual({ status: 'active' });
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/unfreeze');
    expect(lastCall(fetchMock).init.method).toBe('POST');

    const limit = { category: 'fuel', period: 'week', amount: '50.00' };
    fetchMock = stubFetch(jsonResponse(200, limit));
    expect(await client.setLimit('c1', 'fuel', 'week', '50.00')).toEqual(limit);
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/limits/fuel/week');
    expect(lastCall(fetchMock).init.method).toBe('PUT');
    expect(JSON.parse(lastCall(fetchMock).init.body as string)).toEqual({ amount: '50.00' });
    expect(new Headers(lastCall(fetchMock).init.headers).get('Content-Type')).toBe(
      'application/json',
    );

    fetchMock = stubFetch(new Response(null, { status: 204 }));
    await expect(client.removeLimit('c1', 'fuel', 'week')).resolves.toBeUndefined();
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/limits/fuel/week');
    expect(lastCall(fetchMock).init.method).toBe('DELETE');

    fetchMock = stubFetch(jsonResponse(200, [{ id: 't1' }]));
    expect(await client.listTransactions('c1')).toEqual([{ id: 't1' }]);
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/transactions');

    fetchMock = stubFetch(jsonResponse(200, [{ id: 'e1' }]));
    expect(await client.listAudit('c1')).toEqual([{ id: 'e1' }]);
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/audit');
    expect(new Headers(lastCall(fetchMock).init.headers).get('Accept')).toBe('application/json');

    const input = { amount: '12.50', category: 'travel' as const, merchant: 'Rail' };
    fetchMock = stubFetch(jsonResponse(200, decision('approved', 'approved')));
    expect(await client.simulatePurchase('c1', input)).toEqual(decision('approved', 'approved'));
    expect(lastCall(fetchMock).url).toBe('/api/cards/c1/simulate-purchase');
    expect(lastCall(fetchMock).init.method).toBe('POST');
    expect(JSON.parse(lastCall(fetchMock).init.body as string)).toEqual(input);
  });

  test('T-W-5 prefixes every path with the base url', async () => {
    const fetchMock = stubFetch(jsonResponse(200, []));
    await new HttpApiClient('http://localhost:5000').listCards();
    expect(lastCall(fetchMock).url).toBe('http://localhost:5000/api/cards');
  });

  // T-W-5 · PR-02 · web unit
  // Given 404 ProblemDetails on getCard and 400 with errors.amount on setLimit, When called, Then rejects with ApiError(status, problem); simulatePurchase resolves with the decision body on 200, 400 and 404 and rejects with ApiError on 503
  // Catches: inline errors impossible because ApiError lost the ProblemDetails (AC-9, AC-22)
  test('T-W-5 maps error statuses to ApiError but returns decisions for 400/404 on simulate', async () => {
    const client = new HttpApiClient();

    const notFound = { title: 'Not Found', status: 404 };
    stubFetch(jsonResponse(404, notFound, 'application/problem+json'));
    const getError = await client.getCard('zzz').catch((e: unknown) => e);
    expect(getError).toBeInstanceOf(ApiError);
    expect((getError as ApiError).status).toBe(404);
    expect((getError as ApiError).problem).toEqual(notFound);

    const invalid = { title: 'Validation', status: 400, errors: { amount: ['Too big'] } };
    stubFetch(jsonResponse(400, invalid, 'application/problem+json'));
    const setError = await client.setLimit('c1', 'fuel', 'day', '9999999').catch((e: unknown) => e);
    expect(setError).toBeInstanceOf(ApiError);
    expect((setError as ApiError).status).toBe(400);
    expect((setError as ApiError).problem?.errors?.amount).toEqual(['Too big']);

    const input = { amount: '1.00', category: 'fuel' as const, merchant: 'Shell' };
    stubFetch(jsonResponse(200, decision('declined', 'limit_exceeded')));
    expect((await client.simulatePurchase('c1', input)).reason).toBe('limit_exceeded');
    stubFetch(jsonResponse(400, decision('declined', 'invalid_request')));
    expect((await client.simulatePurchase('c1', input)).reason).toBe('invalid_request');
    stubFetch(jsonResponse(404, decision('declined', 'card_not_found')));
    expect((await client.simulatePurchase('c1', input)).reason).toBe('card_not_found');

    stubFetch(new Response(null, { status: 404 }));
    const emptyNotFound = await client.simulatePurchase('c1', input).catch((e: unknown) => e);
    expect(emptyNotFound).toBeInstanceOf(ApiError);
    expect((emptyNotFound as ApiError).status).toBe(404);

    stubFetch(jsonResponse(400, { title: 'Bad', status: 400 }, 'application/problem+json'));
    const problemBody = await client.simulatePurchase('c1', input).catch((e: unknown) => e);
    expect(problemBody).toBeInstanceOf(ApiError);
    expect((problemBody as ApiError).status).toBe(400);
    expect((problemBody as ApiError).problem?.title).toBe('Bad');

    stubFetch(new Response('down', { status: 503 }));
    const simError = await client.simulatePurchase('c1', input).catch((e: unknown) => e);
    expect(simError).toBeInstanceOf(ApiError);
    expect((simError as ApiError).status).toBe(503);
    expect((simError as ApiError).problem).toBeNull();
  });
});

describe('HttpApiClient (002)', () => {
  // T-W-9 · PR-03 · web unit
  // Given a stubbed fetch, When each 002 method is called, Then the method/path of design §7 is used (ids and references URL-encoded; PUT /api/fake-bank/steering with a JSON body) and the JSON body is returned; a 409 ProblemDetails rejects with ApiError whose problem carries code, message, balanceOwed and pendingReference
  // Catches: wrong routes or refusal details lost before the UI (design §7)
  test.todo('T-W-9 maps the payment endpoints and 409 problems');
});
