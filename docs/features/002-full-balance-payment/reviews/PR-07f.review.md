# PR-07f review: Fix BUG-009, activity shows a purchase at its processor time (as 001) (mode `pr-light`)

- PR: https://github.com/cardlab-sim/cardlab/pull/46 · branch `pr/002-full-balance-payment/07f-fix-bug-009` @ `ab0954e` · base `feat/002-full-balance-payment`
- Inputs: `prs/PR-07-fix-BUG-009.md`, `bugs.md` BUG-009, `02-tech-design.md` §5 `ICardStore` row (line 381, erratum BUG-009) and decisions log line 745, `02-design-digest.md` DEC-12, PR diff, PR body.

## Verdict: **APPROVE WITH FIXES**

Counts: blocker 0 · major 0 · minor 2 · nits (one line at the end).

The fix applies the erratum rule the same way in all three implementations:

| Implementation | Shown time `At` | Ordering key | Where |
|---|---|---|---|
| SQL store | purchase `OccurredAt`; payment `CreatedAtUtc` | purchase `DecidedAtUtc`, payment `CreatedAtUtc` | `SqlCardStore.cs:194-220` |
| In-memory store | same | same | `InMemoryCardStore.cs:264-278` |
| Web fake | purchase `at` (= `transaction.occurredAt`); payment `at` (confirm time) | purchase `decidedAt` (stamped `now`); payment `at` | `fakeApiClient.ts:78-79, 359-361, 777-780` |

Points the orchestrator asked about:

- **`take` still returns the newest rows by the ordering key.** The SQL pre-queries still take `take` rows by `DecidedAtUtc` (`SqlCardStore.cs:185`) and by `CreatedAtUtc` (`:192`). The merge orders by the same keys (`Placed`) and then takes `take` (`:220`). The newest `take` rows of the union are always among the newest `take` rows of each side, so the result is correct. The in-memory store sorts the whole list by `Placed`, then takes `take`. One gap: no test pins the SQL pre-query key now that the two keys can differ (F1).
- **No other consumer relied on `At` being the decision time.**
  - `PaymentDtoMapper.ToDto` (`PaymentDtoMapper.cs:34-35`) passes `At` through.
  - `CardEndpoints.cs:184` returns the rows in store order.
  - `CardholderViewService.cs:54` takes the first 10 rows as the store orders them. It never re-sorts or reads `At`.
  - The web pages `paymentSections.tsx:25` and `CardholderViewPage.tsx:17` only format `row.at`. Neither sorts.
  - The cardholder view's "Recent card activity" therefore also shows the processor time. That is consistent with the erratum, which is stated on `ListActivityAsync` itself (design §5 line 381) and which the cardholder view reads (design line 55).
  - `ICardStore.cs:87` ("Purchases (by `DecidedAtUtc`) and payments (by `CreatedAtUtc`) merged") is still true about the ordering.
- **The contract test pins both halves.** `PaymentStoreContractTests.cs:331-347`:
  - The purchase is stamped at T0+2, before the payment at T0+3:30, and decided at T0+5, after it.
  - The test asserts the order `[Purchase, Payment]` and the literals `At` = T0+2 and T0+3:30.
  - Mutation "show `DecidedAtUtc`" puts T0+5 in `At`, so the test fails.
  - Mutation "order by `OccurredAt`" or "order by `At`" puts the payment first, so the test fails.
  - It runs on both stores: in-memory in Domain.Tests, and SQL as `SqlPaymentStoreContractTests` (verified by name below).
- **API test.** `AdminPaymentEndpointsTests.cs:91-106` asserts the literal stamp 2026-10-24T23:30:00Z against a decision time 2 days later. Showing the decision time fails it.
- **Web fake test.** T-W-10 (`fakeApiClient.test.ts:588-616`) shows "Late" at its literal stamp `2026-06-01T08:00:00Z` and above the payment. Ordering by `at` would move "Late" to the bottom, and showing `decidedAt` would show 12:10, so either mutation fails the test. One claim in its *Catches* line is not true (F2).
- Doc comments are true: `PaymentRecords.cs:90`, the remarks of `SqlCardStore.ListActivityAsync`, and the class TSDoc of `FakeApiClient`. There is no speculative generality. The extra `decidedAt` field is the minimum the fake needs.

## Evidence (fresh, PR head `ab0954e`, coder worktree `agent-af26a35d4ef8f2c92`, clean)
- Scope: `doc_lint.py scope … --base origin/feat/002-full-balance-payment --head origin/pr/002-full-balance-payment/07f-fix-bug-009` → **PASS (7 changed, 7 owned, 0 errors, 0 warnings)**.
- `commands.unit`:
  - `dotnet test tests/CardLab.Domain.Tests` → **283 passed, 0 failed, 0 skipped**.
  - `npm test --prefix web -- --run` → **13 files, 123 tests passed**.
- `npm run lint --prefix web` → **clean** (exit 0). `npm run typecheck --prefix web` → **clean**. `dotnet format CardLab.sln --verify-no-changes` → **exit 0**.
- Integration (LocalDB), filtered:
  - `SqlPaymentStoreContractTests.Late_purchase_shows_its_processor_time_ordered_by_decision` → passed.
  - `AdminPaymentEndpointsTests.Activity_shows_a_purchase_at_its_processor_time` → passed.
  - Wider filter (activity + cardholder) → **5 passed, 1 skipped (pre-existing todo), 0 failed**.
  - The orchestrator's full run: 121 passed / 0 failed / 4 skipped.
- E2E: I did not re-run it (pr-light).
  - The orchestrator ran 001's `formatting.spec.ts` on chromium and firefox on an isolated port: passing.
  - The PR body reports the full suite: 15 passed, 17 skipped (PR-08 todo specs), 0 failed.
- I did no mutation runs (pr-light). The mutation reasoning above is by hand against literal expectations. The PR body reports the brief's two SQL mutations red.

## Findings

| # | Severity | Where | Finding | Why it matters | Fix | Check |
|---|---|---|---|---|---|---|
| F1 | minor | `src/CardLab.Infrastructure/Sql/SqlCardStore.cs:185`; `tests/CardLab.TestKit/Contracts/PaymentStoreContractTests.cs:331-347` | The SQL purchase pre-query `.OrderByDescending(t => t.DecidedAtUtc).Take(take)` is not pinned by any test now that `OccurredAt` and `DecidedAtUtc` can differ. The new contract case uses `take` 10 with one purchase. P-10's `take` 2 case builds purchases with `At(...)` only, where the builder sets `DecidedAtUtc` = `OccurredAt` (`Builders.cs:261`). If the pre-query key changes to `OccurredAt`, every test stays green. | That change looks like a harmless "consistency" edit. With it, a card whose late purchase was decided recently but stamped long ago would lose that purchase from `?take=` and from the cardholder view's 10 rows. The in-memory store has no pre-query, so only the SQL run of the contract can catch this. | Extend `Late_purchase_shows_its_processor_time_ordered_by_decision`: in the same unit of work, also append `new TransactionBuilder().Amount("20.00").At(T0.AddMinutes(4)).Build()` (stamped and decided at T0+4). Assert the full list kinds `[Purchase, Purchase, Payment]` with `At` = T0+2, T0+4, T0+3:30. Then add `var newest = await store.ListActivityAsync(card.Id, 1); Assert.Equal(T0.AddMinutes(2), Assert.Single(newest).At);`. | Run `dotnet test tests/CardLab.Integration.Tests --filter "FullyQualifiedName~Late_purchase_shows"`: green. Then mutate `SqlCardStore.cs:185` to `.OrderByDescending(t => t.OccurredAt)`: `SqlPaymentStoreContractTests.Late_purchase_shows_its_processor_time_ordered_by_decision` must fail (single row `At` = T0+4). Revert. |
| F2 | minor | `web/src/api/fakeApiClient.test.ts:587` | The *Catches* line claims "rows in insertion order". The test cannot catch that. The fake `unshift`s every item at `new Date()` (`fakeApiClient.ts:210, 361, 473`), and the test only moves the clock forward. Insertion order is therefore identical to the `decidedAt`/confirm-time order. If you delete the sort at `fakeApiClient.ts:777-780`, T-W-10 still passes. | A *Catches* line that names a break the test cannot detect misleads the next reader about what is protected (writing-good-tests, "Name the break"). The claim was carried over from PR-03f and rewritten in this PR. | Choose one. (a) Remove "rows in insertion order; " from the comment. (b) Make it true: after the "Late" purchase, `vi.setSystemTime(new Date('2026-07-01T12:01:00Z'))`, call `fake.addPurchase('ops-1', '5.00')`, and expect a `['Card purchase', '2026-07-01T12:01:00.000Z']` row between "Early" and the seeded "Card purchase". Then update `balanceOwed` to `'180.50'`. | (a) `grep -n "rows in insertion order" web/src/api/fakeApiClient.test.ts` → no match. (b) `npm test --prefix web -- --run src/api/fakeApiClient.test.ts` green; then replace `fakeApiClient.ts:777-780` with `return entries;`: T-W-10 must fail. |

Nits: `ICardStore.cs:87`'s summary could add "shown at `OccurredAt` / `CreatedAtUtc`" so the port states both halves without the reader opening `ActivityEntry` (file not owned here; fold into the feature review if anyone touches it).

No `upstream:` items.

## Fixes verified (orchestrator, 2026-11-07, head 88b895f)
- Fresh: web unit 123 passed; scope PASS. Earlier on ab0954e: Domain.Tests 283, Integration.Tests 121 / 0 failed; 001 formatting.spec T-E-6 green on chromium and firefox (isolated port).
- F1: SQL purchase pre-query ordered by `OccurredAt` (`SqlCardStore.cs:185`) fails `SqlPaymentStoreContractTests.Late_purchase_shows_its_processor_time_ordered_by_decision`. Verified.
- F2: per coder report (sort removed in the fake → T-W-10 fails). The T-W-10 balance assertion is 180.50 (includes the added £5.00 purchase).
- Nit (`ICardStore.cs:87` summary) → feature review.
