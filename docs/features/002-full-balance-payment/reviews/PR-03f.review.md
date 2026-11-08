# PR-03f review (pr-light): Fix BUG-007, shared web fake feeds simulated purchases into activity and balance

- PR: https://github.com/cardlab-sim/cardlab/pull/44 · branch `pr/002-full-balance-payment/03f-fix-bug-007` · reviewed at `d8ee39a`
- Base: `feat/002-full-balance-payment` · brief `prs/PR-03-fix-BUG-007.md` · ledger BUG-007 · background `reviews/PR-07.review.md` F5
- Inputs read: brief, diff, R-1 / AC-1 (`01-requirements.md:151,183`), design digest DEC-1/DEC-12/DEC-15, design §5 `ICardStore.ListActivityAsync` row (`02-tech-design.md:381`), `SqlCardStore.ListActivityAsync` (for fake fidelity)

## Verdict: **APPROVE WITH FIXES**

Counts: blocker 0 · major 1 · minor 1 · nits 3.

The fake now matches R-1: owed = approved purchases − completed payments, and declined purchases are listed but not counted. `withQuoteOrigin` is one-shot and reaches the quote, receipt, record, payments list and activity. `ActivityFake` and `paymentsOverride` are gone, and the CardDetail fixtures are now consistent (£150.50 opening − £150.50 paid + £20.00 + £12.50 = £32.50). No coverage was lost. One fidelity gap remains: the activity **timestamps** the fake sorts by are not the ones the design and the real store use, and a new test pins the wrong one (F1).

## Evidence (run by me at `d8ee39a`, in the PR worktree)

- Scope: `doc_lint.py scope … --base origin/feat/002-full-balance-payment --head origin/pr/…/03f-fix-bug-007`: **PASS** (3 changed, 3 owned, 0 errors).
- Production code untouched: the only non-test file changed is `web/src/api/fakeApiClient.ts`, and it is imported only by `*.test.tsx` files.
- `npm test --prefix web -- --run`: **13 files, 123 passed, 0 failed**. `npm run lint --prefix web`: eslint + prettier clean. `npm run typecheck --prefix web`: tsc 0 errors. (The .NET suites are unaffected because no C# changed. I did not re-run them.)
- Mutations on the fake. Each one fails at least one test, and I reverted each afterwards (worktree clean):
  | Mutation | Caught by |
  |---|---|
  | `owed()` counts declined purchases | T-W-10 declined purchase (`fakeApiClient.test.ts:570`) |
  | drop the `sort` in `activityEntries` | T-W-10 ordering (`:588`) |
  | `nextQuoteOrigin` not reset after `createQuote` | T-W-10 origin one-shot (`:619`) |
  | receipt + payments list origin hard-coded `cardholder_view` | T-W-10 origin + CardDetail T-W-8 smoke badge |
  | activity payment origin hard-coded | T-W-10 origin + CardDetail T-W-8 smoke badge |
  | ties reversed (`entries.reverse().sort(…)`) | 5 fake tests + CardDetail T-W-8 "shows balance, activity and payments" |
- PR-07 review mutations, re-run on `CardDetailPage.tsx`:
  - removing `checked.current.add(...)` fails `T-W-8 does not re-check an unanswered pending payment`.
  - restoring the old `seq.current === mine` post-check guard fails `T-W-8 a user action during the auto check does not hide the settled payment`.

## Spec and coverage check

| Item | Where | Status |
|---|---|---|
| AC-1 / R-1: owed = approved − completed | `fakeApiClient.ts:622-630`; tests `:546` (150.50 + 12.50 → `163.00`, both `getCard` and `getCardholderView`), `:570` (declined → stays `150.50`), `:588` (→ `25.00`) | met (floor: see nit 1) |
| Activity newest first, stable ties | `fakeApiClient.ts:765-766` (stable `Array.prototype.sort`, insertion = newest first) | met for ordering, but uses the wrong timestamps (F1) |
| DEC-15 origin per quote → receipt/record/list/activity | `fakeApiClient.ts:210-214, 406-408, 558, 586, 731, 760`; test `:619` | met |
| T-W-3 simulate → declined row first | `CardDetail.test.tsx:70-106` now on the shared fake | kept |
| T-W-8 balance/activity/payments | `CardDetail.test.tsx:448-505`: £32.50 balance, 4 rows in order, payment link, open quote £32.50 | kept, now consistent |
| T-W-8 smoke badge | `CardDetail.test.tsx:508-519`: real `withQuoteOrigin` flow, no override | kept |
| T-W-8 auto-check / no loop / action during check | `CardDetail.test.tsx:524-581` | kept (both PR-07 mutations still caught) |
| T-W-9 UK formatting of limit + activity time | `CardDetail.test.tsx:168-198` | kept, but depends on F1's divergent `at` |

## Findings

| # | Severity | Where | Finding | Why it matters | Fix | Check |
|---|---|---|---|---|---|---|
| F1 | major | `web/src/api/fakeApiClient.ts:349-357` (purchase `at: transaction.occurredAt`), `:753` (payment `at: rec.effectiveAt ?? rec.createdAt`); pinned by `fakeApiClient.test.ts:588-613` and relied on by `CardDetail.test.tsx:178-189` | The design defines the activity time as follows: purchases by `DecidedAtUtc`, payments by `CreatedAtUtc`, newest first (`02-tech-design.md:381`, implemented in `SqlCardStore.ListActivityAsync`). The fake uses different times. (a) A simulated purchase uses the **client-supplied `timestamp`**, so the backdated "Old" purchase sorts last in the new ordering test. The real API decides it at 12:05 and lists it first. (b) A payment uses the **settlement time**, and for a pending payment it uses the **quote** time. The real row keeps its confirm time, and it does not move when a status check settles it later. | BUG-007 exists to make this fake one model of the real API. As written, the fake and its new test fix a behaviour the API does not have. A future page test that uses a backdated purchase, or a pending payment settled by a check, would pass against the fake while the real page orders the rows differently. The fake would hide ordering bugs, which is the kind of drift F5 was raised about. | (1) At `:349-357`, set the activity item's `at: new Date().toISOString()` (decision time). Keep `transaction.occurredAt` for `listTransactions`. (2) Give the payment activity item an `at`: change the type to `{ kind: 'payment'; reference: string; at: string }`, push `{ kind: 'payment', reference, at: now }` at `:463`, and use `item.at` at `:753`. (3) In the doc comment at `:119`, say "newest first by decision time (purchases) and confirm time (payments), like design §5". (4) Rewrite the test at `:588-613` to move time with `vi.setSystemTime` between steps. Keep one purchase with a backdated `timestamp` and assert it is listed by decision time. Add one assertion that a pending payment confirmed at T1 and settled by `checkPayment` at T2 still has `at` = T1. (5) In T-W-9 (`CardDetail.test.tsx:177-189`), pin the time with `vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-24T23:30:00Z') })` in try/finally, as T-W-8 already does, instead of passing `timestamp`. The asserted `25 Oct 2026 00:30` stays. | `npm test --prefix web -- --run` is green. Mutation A: restoring `at: transaction.occurredAt` at `:351` must fail the rewritten ordering test. Mutation B: restoring `at: rec?.effectiveAt ?? rec?.createdAt ?? ''` must fail the new pending-then-settled assertion. |
| F2 | minor | `web/src/pages/CardDetail.test.tsx:446` | The Given/Then comment of `T-W-8 shows balance, activity and payments` still says `"Balance owed £20.00" (the opening £150.50 was paid, a £20.00 purchase followed)`. The test now asserts £32.50, with the £12.50 Shell purchase. | Test comments are the spec readers trust. This one now describes the inconsistent fixture that BUG-007 removed. | Change the comment to: `… Then the heading shows "Balance owed £32.50" (the opening £150.50 was paid; £20.00 and a £12.50 Shell purchase followed); the open quote is £32.50; …`. | `grep -n "Balance owed £20.00" web/src/pages/CardDetail.test.tsx` prints nothing. |

Nits (optional):
1. `owed()` (`fakeApiClient.ts:622-630`) has no floor at zero, unlike R-1 and the doc comment. Through the fake's API it cannot go below zero: confirm requires owed = quote amount, and purchases only add. Adding `Math.max(0, …)` would make the claim literal.
2. The doc comment at `:119` says "purchases (any outcome)". `invalid_request` / `card_not_found` decisions are not listed, which matches the API, so it should say "approved and declined purchases".
3. The `''` fallback for a payment's `at` (`:753`) would put `NaN` into the sort comparator. This is unreachable today, and it disappears with F1(2).

## upstream
None. The design is clear (`02-tech-design.md:381`); F1 is a harness fidelity fix.

## Routing
- Coder (same round): F1 and F2. Each has a mechanical check above, so **no `recheck` is needed**. The orchestrator verifies with the suite and the two F1 mutations.

## Fixes verified (orchestrator, 2026-11-07)
- Fresh web unit: 123 passed; scope PASS (3/3 owned).
- F1: purchase activity time restored to the client `timestamp` (`fakeApiClient.ts:351`) fails the activity-order test. Verified. Payment confirm-time mutation per coder report.
- F2 and nits per coder report.
