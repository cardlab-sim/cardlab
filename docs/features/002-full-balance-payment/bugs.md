# Bug & fix ledger: Full balance payment (CL-3, slice 1)

<!-- Append-only. Never delete a row; change Status. The SessionStart hook reminds you of every
row whose Status is new, triaged, approved, in-progress or deferred. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-02, F3) | S3 | AC-3 | Store contract P-1 expects the test's own linked account id, which the shared SQL test database cannot return | plan | fix-now (option A) | verified | build (PR-02 fix round) | | 2026-11-07 |

| BUG-002 | reviewer (PR-04, F4) | S3 | NFR-2, R-9 | Client disconnect cancels the bank call (budget linked to request token), contrary to design §13 | design | fix-now (option A: code matches design) | verified | build (PR-04 fix round) | | 2026-11-08 |
| BUG-003 | reviewer (PR-04, F3) | S4 | R-9 | Status check awaiting an in-flight confirm is undocumented and unbounded | design | fix-now (option A: record + cap at 10 s) | verified | build (PR-04 fix round) | | 2026-11-08 |
| BUG-004 | reviewer (PR-05, F4) | S2 | R-15 | Steered "time out, not received" failure is not marked simulated | design | fix-now (option A: amend design + code) | verified | build (PR-05 fix round) | | 2026-11-08 |
| BUG-005 | reviewer (PR-05, F8) | S4 | DEC-15 | Origin stored as enum name, design text said deploy_smoke | design | fix-now (amend design text) | verified | — | | 2026-11-08 |
| BUG-006 | orchestrator (feature CI run 42410267102) | S4 | AC-10 | Disconnect test flaky: in-flight registry entry removed after the confirm returns | code | fix-now (option A) | verified | build (PR-04f, wave 3) | | 2026-11-07 |
| BUG-007 | reviewer (PR-07, F5) | S4 | R-1 | Shared web FakeApiClient doesn't feed simulated purchases into activity/balance; PR-07 test-local ActivityFake duplicates it with inconsistent data | test-harness | fix-now (small fix PR) | verified | build (PR-03f, wave 4) | | 2026-11-08 |
| BUG-008 | reviewer (PR-06, F4) | S4 | AC-9, AC-10 | Repeated/parallel confirm receipt time differs by < 1 ms on the wire (full-tick vs stored ms) | code | fix-now (fold into PR-04f) | verified | build (PR-04f) | | 2026-11-08 |
| BUG-009 | coder (PR-08, 001 E2E formatting.spec T-E-6) | S2 | 001 AC-38, R-7 | Admin activity shows a purchase at decision time instead of the processor timestamp (001 regression) | design | fix-now (B: exactly as 001) | verified | build (PR-07f, wave 4) | | 2026-11-07 |
| BUG-010 | reviewer (feature, F1) | S1 | R-15, R-14, AC-20, AC-33 | Steered "no answer" outcomes recorded as a real bank non-answer (simulated = false) | design | fix-now (option A: design erratum + code) | verified | validate (PR-09 #48) | | 2026-11-08 |
| BUG-011 | reviewer (feature, F2) | S2 | R-15, G-1, AC-26 | Demo fake bank can only drain; deploy smoke depends on mutable demo state | requirements | defer (option C: §17 note) | deferred | — (revisit before Azure returns) | https://github.com/cardlab-sim/cardlab/issues/47 | 2026-11-08 |
| BUG-012 | validator | S3 | AC-9, R-7, R-12 | Repeated confirm or receipt reload after a later purchase shows the current balance, not the original receipt | | | new | | | 2026-11-08 |
<!-- Severity: S1 blocker · S2 major · S3 minor · S4 cosmetic
     Root-cause layer: requirements | design | plan | code | test-harness | environment | needs-debugger
     Decision: fix-now (option A/B) | defer | wont-fix, with the human's words in the section below
     Status: new → triaged → approved → in-progress → fixed → verified   |  deferred  |  wont-fix -->

## BUG-001
**Repro:** PR-02 (#36) contract `PaymentStoreContractTests` P-1 asserts `GetLinkedAccountAsync()` returns the account passed to `CreateStoreAsync`, including its id.
**Expected / actual:** the suite must pass unchanged against PR-05's SQL adapter. On SQL each test class shares one database holding the seeded linked account plus one per `CreateStoreAsync`, and the port has no card or business scope, so the read cannot return "the test's" account deterministically.
**Evidence:** reviews/PR-02.review.md F3 (`Contracts/PaymentStoreContractTests.cs:38-50`, `ICardStore.cs:63-67`, `SqlTestDatabase.cs:57-64`).
### Triage (reviewer)
Root cause: plan. The PR-02 brief's P-1 "Then" (same id) does not fit an unscoped "the linked account" read on a shared test database. Not a coding error in PR-02.
Options: (A) P-1 asserts the fields every row shares (builder defaults = seed: DisplayName, LastFour, BankRef, AuthorityBasis) plus a non-empty id; amend P-1 in prs/PR-02.md. (B) PR-05 gets a dedicated SQL test class/database for P-1 (plan work). (C) scope the port by card (design change).
### Decision (human, 2026-11-07)
"A. Loosen P-1 (Recommended)": P-1 asserts the fields every linked-account row shares (DisplayName, LastFour 7731, BankRef = Seed.BankAccountRef, AuthorityBasis) plus a non-empty id. The P-1 rule lives in the PR-02-owned stub comment of `PaymentStoreContractTests.cs`, so the PR-02 coder changes comment and test together; no brief text names the id.
### Resolution
PR-02 (#36) fix round, head a03d3ff: P-1 asserts the shared fields plus a non-empty id; reviewer recheck PASSED (mutations: wrong AuthorityBasis or empty BankRef fail P-1, random Id passes). Verified 2026-11-07: P-1 passes unedited on SQL in PR-05 (#40, head 3504f23), Integration.Tests 91 passed / 0 failed (orchestrator fresh run).

## BUG-002
**Repro:** PR-04 `PaymentService.cs:361-370`: the bank budget is `CreateLinkedTokenSource(ct)` + `CancelAfter`; a client disconnect cancels the debit call.
**Expected / actual:** design §13 said the settlement still runs after a disconnect; actually the payment stays Pending with no Bank* event until the next check.
**Evidence:** reviews/PR-04.review.md F4.
### Triage (reviewer)
design: §13 text promises what the linked budget cannot do. Options (a) reword §13, (b) unlink the budget.
### Decision (human, 2026-11-07)
"A. Code matches design (Recommended)": unlink the bank budget from the request token. Design §13 erratum states it explicitly; PR-04 brief amended.
### Resolution
PR-04 (#39, head 4052ef1): unlinked bank budget; `Client_disconnect_never_cancels_the_debit_and_the_payment_settles`. Orchestrator verified (reviews/PR-04.review.md). Moves to verified after validation (T-I timeout/idempotency).
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): held the fake bank's applock for 4 s with `sqlcmd`. The confirm client gave up after 1 s, and the payment still settled `completed` (`bank_completed`, bank −£150.50) once the lock was released. The regression test is green.

## BUG-003
**Repro:** PR-04 `PaymentService.cs:176-194`: `CheckStatusAsync` awaits an in-flight confirm before asking the bank (coder addition; correct, closes an R-9 hole).
**Expected / actual:** not in DEC-5 / Flow 2; worst-case wait ~20-35 s, unbudgeted.
**Evidence:** reviews/PR-04.review.md F3.
### Triage (reviewer)
design: keep the behaviour, record it, decide on a bound.
### Decision (human, 2026-11-07)
"A. Record + cap at 10 s (Recommended)": DEC-5 / Flow 2 erratum; on expiry `StatusCheckUnanswered`, W-10, no bank call.
### Resolution
PR-04 (#39, head 4052ef1): check wait capped by its own 10 s budget; removing the cap fails `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget`. Orchestrator verified.
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): a check sent 1 s into a hanging confirm (`timeout_debit_applied`, real 10 s budget) returned at 10,104 ms, after the confirm (10,086 ms). It did not ask the bank early, and exactly one BankEntry exists. `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget` is green. The 10 s cap itself cannot be reached with the shipped fake, so the unit test is its proof.

## BUG-004
**Repro:** fake bank steered to *time out, not received* → nothing recorded → status check answers `NotReceived(Simulated: false)` → payment Failed, not marked simulated.
**Expected / actual:** R-15: outcomes produced under steering are marked simulated in the payment record.
**Evidence:** reviews/PR-05.review.md F4 (`SqlFakeBank.cs:100`, `:180-184`); design Flow 6 "commit nothing".
### Triage (reviewer)
design: Flow 6 / DEC-6 omitted recording the steered not-received request.
### Decision (human, 2026-11-07)
"A. Amend design + code (Recommended)": record `BankDebitRequest` Outcome NotReceived, Simulated = 1, no ledger entry, no balance change; status check returns NotReceived(simulated). Design Flow 6 / §6 erratum.
### Resolution
PR-05 (#40, head 4ab098e): simulated NotReceived request row under steering; status check reads it back. Orchestrator mutation: recorded NotReceived mapped to not-simulated fails 2 integration tests. Moves to verified after validation (AC-33 / R-15 path).
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): steered `timeout_not_received`, confirm (pending), then check: the payment failed `not_received` with `payment.simulated = true`, and the event `status_check_not_received` is simulated. `Not_received_is_simulated_only_when_steered` is green.

## BUG-005
**Repro:** `SqlPaymentWriter.cs:125` stores `Origin` as the enum name (`DeploySmoke`); design §6 / DEC-15 said `deploy_smoke`.
**Expected / actual:** docs and data disagree; wire names already `deploy_smoke`.
**Evidence:** reviews/PR-05.review.md F8.
### Triage (reviewer)
design text only.
### Decision (human, 2026-11-07)
"Amend design text (Recommended)": stored as enum name, wire `deploy_smoke`. No code change.
### Resolution
Design DEC-15 / §6 / §4 text, design digest and ADR-007 amended 2026-11-07. Check: `grep -n "Origin = deploy_smoke" 02-tech-design.md` → no hits.
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): `PaymentQuote.Origin` holds `CardholderView` / `DeploySmoke` (SQL). The wire sends `deploy_smoke` only for the exact header `deploy-smoke` (`Deploy-Smoke` gives `cardholder_view`). The local smoke record shows "origin: smoke". `OriginMarkerTests` is green.

## BUG-006
**Repro:** feature-branch CI `backend` (run 42410267102, head d32241c): `PaymentServiceTests.Client_disconnect_never_cancels_the_debit_and_the_payment_settles` fails at line 382 (`Assert.False(f.InFlight.TryGet(...))`); passes locally.
**Expected / actual:** the registry is clear once the confirm returns; actually `InFlightPayments.RunAsync` removes the entry in an asynchronous continuation after `ConfirmAsync` has already returned.
**Evidence:** `PaymentService.cs:185` `completion.SetResult(result)` then return; `InFlightPayments.RunAsync` `await start()` → `Remove` (continuation, `RunContinuationsAsynchronously`).
### Triage (orchestrator)
code (PR-04): race in the test-visible registry cleanup; production impact nil (a check finding the stale entry awaits a completed task, then re-reads state).
### Decision (human, 2026-11-07)
"A. Small fix PR now (Recommended)": brief prs/PR-04-fix-BUG-006.md, wave 3.
### Resolution
PR-04f (#41): `InFlightPayments.Complete`/`Fail` release the entry before completing; swap mutation fails 3/3; 200-repeat stability run 0 failures. Verified: feature CI run 42416721004 (head e6729da) `backend` green, unit and integration.

## BUG-007
**Repro:** `web/src/pages/CardDetail.test.tsx:604-638` (`ActivityFake`, `paymentsOverride`) subclasses the shared `FakeApiClient` (`web/src/api/fakeApiClient.ts`, PR-03) because `simulatePurchase` never reaches `listActivity` or the balance and `origin` is hard-coded.
**Expected / actual:** one consistent shared fake; actually T-W-8 asserts "Balance owed £20.00" beside £32.50 of approved purchases (`:462`), rows concatenated, not time-ordered.
**Evidence:** reviews/PR-07.review.md F5.
### Triage (reviewer)
test-harness: follow-up change to the shared fake (not owned by PR-07); then drop `ActivityFake`.
### Decision (human, 2026-11-07)
"Small fix PR now (Recommended)": make the shared FakeApiClient consistent and delete PR-07's ActivityFake.
### Resolution
PR-03f (#44): shared FakeApiClient models R-1 and design §5 activity order; ActivityFake removed; mutations verified.
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): `grep ActivityFake web/src` finds nothing. The shared fake's `simulatePurchase` now feeds `listActivity` and the balance (T-W-10; T-W-8 asserts a consistent £32.50). Web: 123/123.

## BUG-008
**Repro:** first confirm's receipt `Time` carries the clock's (or fake bank's) full-tick instant; repeats and reads return the stored datetime2(3) value; T-I-4 needs a 1 ms tolerance; parallel confirms differed in ~1/19 runs.
**Expected / actual:** AC-9/AC-10 "the same outcome": identical receipts on the wire; actually sub-ms difference (screen shows minutes, unaffected).
**Evidence:** reviews/PR-06.review.md F4.
### Triage (reviewer)
code: truncate instants to ms at the source in PaymentService (`now` in ValidateAsync/SettleAsync, the bank's debit time).
### Decision (human, 2026-11-07)
"Fold into PR-04f (Recommended)"; then T-I-4 and T-I-5 assert exact equality.
### Resolution
PR-04f (#41): instants truncated to ms at source; T-I-4/T-I-5 exact equality; mutation verified.
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): 10 parallel confirms plus one later repeat gave one identical `time` (`…:33.926+00:00`), and the bodies differ only in `repeated`. T-I-4 and T-I-5 assert exact equality and are green.

## BUG-009
**Repro:** 001 E2E `tests/e2e/src/formatting.spec.ts` "T-E-6 limit and transaction time" (chromium and firefox): a processor purchase stamped 2026-10-24T23:30:00Z shows "7 Nov 2026 21:37" (the decision time) instead of "25 Oct 2026 00:30".
**Expected / actual:** 001 showed `OccurredAt` (the processor's timestamp) and ordered by `DecidedAtUtc`; 002's `ListActivityAsync` (design §5) shows `DecidedAtUtc`.
**Evidence:** `SqlCardStore.ListActivityAsync` maps `At = DecidedAtUtc`; 001 `ListTransactionsAsync` orders by `DecidedAtUtc` and its DTO carries `occurredAt`.
### Triage (orchestrator)
design: §5 named decision time for the activity entry without separating the shown time from the order.
### Decision (human, 2026-11-07)
First answered "A. Processor time (Recommended)" on an option table that wrongly described A as 001's behaviour. After the orchestrator's correction (001 showed processor time, ordered by decision time), the human chose "B. Exactly as 001 (Recommended)": show purchases at the processor timestamp, order by decision time, payments by confirm time. Design §5 erratum.
### Resolution
PR-07f (#46): SQL and in-memory stores and the web fake show purchases at `OccurredAt`, ordered by `DecidedAtUtc`; contract case on both stores; 001 formatting.spec T-E-6 green on both browsers (orchestrator, isolated port). Verified: feature CI run 42419754713 (head 8335420) green, e2e included.

## BUG-010
**Repro:** steer the fake bank to *time out, debit applied*, *time out, not received* or *Next status check: does not answer*, then confirm (or check). The record shows `bank_unanswered` / `status_check_unanswered` with `simulated: false`, and `payment.simulated == false`.
**Expected / actual:** R-15 and DEC-7 (02-tech-design.md:167) say steered outcomes are marked simulated. Actually, the steered hang ends in a plain `OperationCanceledException`, so `PaymentService.cs:419-421` returns `Unknown(false)` and `:501` appends the event unmarked. The SPA `FakeApiClient` marks them simulated, so the fake and the real API disagree.
**Evidence:** reviews/feature.review.md F1 (head aa4c956): `SqlFakeBank.cs:72,114`, `PaymentService.cs:419-421,501`, `PaymentDtoMapper.cs`, `fakeApiClient.ts:494,509-511`.
### Triage (reviewer, feature review)
Root cause: design. The §5 `ICurrentAccountBank` contract has no way to tell a steered no-answer from a real one. Proposed erratum: a steered no-answer ends with `SimulatedNoAnswerException : OperationCanceledException` (`CardLab.Domain.Ports`) when the caller's token fires. Code: the adapter and TestKit hangs throw it, the service maps it to `Unknown(simulated: true)` and passes the flag to the event, and the DTO falls back to `Events.Any(e => e.Simulated)`. Test: `Steered_no_answer_is_recorded_as_simulated` plus a mutation.
### Decision (human, 2026-11-08)
"A. Erratum + code (Recommended)". The erratum landed in 02-tech-design.md §5, DEC-7, §9 and §15, plus the digest and ADR-007; Gate 2 stays approved. Code goes in PR-09 (`prs/PR-09-fix-BUG-010.md`), together with feature-review F4, F6, F7, F8 and F9.
### Resolution
PR-09 (#48, merged at 88d84fb): `SimulatedNoAnswerException` from both fakes is mapped to `Unknown(simulated: true)` on the `BankUnanswered` / `StatusCheckUnanswered` events, and the record DTO falls back to the events for a pending payment.
Verified (validator, 2026-11-08, head 5cdf614, 04-validation.md): the running system (real 10 s budget) records a steered debit no-answer as `bank_unanswered` with `simulated true` and `payment.simulated true`, and a steered `does_not_answer` check as `status_check_unanswered` with `simulated true`. An unsteered no-answer (bank applock held) stays `simulated false`. Mutation in a throwaway worktree (`PaymentService.cs:426` → `Unknown(e is null)`): `Steered_no_answer_is_recorded_as_simulated` (unit + integration) and `Steered_unanswered_check_is_recorded_as_simulated` go red, 3 failures. All are green on HEAD.

## BUG-011
**Repro:** on `demo`, each completed payment lowers the fake bank's £25,000.00 balance, and nothing can refill it (R-15 forbids balance edits; the reset is off on `demo`, AC-37). One £24,999 purchase plus payment and every later demo payment fails with W-3. The deploy smoke also fails if a visitor freezes Ops van or sets a Fuel limit.
**Expected / actual:** G-1 and AC-26 need a working payment on `demo`. Actually, demo state only degrades.
**Evidence:** reviews/feature.review.md F2: `DatabaseProbe.cs:66-76`, `tests/e2e/src/smoke/pay.spec.ts:18-20`, R-15, DEC-14. Context 2026-11-08: Azure is parked on `main` (`deploy.yml` is `workflow_dispatch` only, DORA-lite uses `dora-deploy.yml`), so no `demo` deploy or smoke runs today.
### Triage (reviewer, feature review)
Root cause: requirements (R-15 forbids the only remedy). Options: (a) amend R-15 with a labelled *restore opening balance* action that posts a simulated credit, and have the smoke call it; (b) keep R-15, have the smoke fail fast with "demo fake bank drained", and add a runbook; (c) record it in design §17 only. With any option, the smoke should reset Ops van's freeze and Fuel limit first.
### Decision (human, 2026-11-08)
"C. §17 note + defer (Recommended)". Azure is parked, so no `demo` runs. Design §17 has the risk row; deferred as https://github.com/cardlab-sim/cardlab/issues/47. Revisit before Azure returns (options a/b in the issue).

## BUG-012
**Repro:** on a host with `CardLab:TestEndpoints=true` (`B=http://localhost:5080`, `OPS=` the Ops van id from `GET $B/api/cards`):
1. `curl -X POST $B/api/test/reset`
2. `curl -X POST $B/api/cards/$OPS/simulate-purchase -H 'Content-Type: application/json' -d '{"amount":"150.50","category":"fuel","merchant":"Repro"}'`
3. `REF=$(curl -s -X POST $B/api/cardholder/cards/$OPS/quotes | jq -r .reference)`, then `curl -X POST $B/api/cardholder/cards/$OPS/payments/$REF/confirm`. The receipt is `completed`, `balanceOwed "0.00"`, "… Your card balance is now £0.00. Reference …".
4. Repeat step 2 with `"amount":"20.00"`.
5. `curl -X POST $B/api/cardholder/cards/$OPS/payments/$REF/confirm` (a repeat) and `curl $B/api/cardholder/cards/$OPS/payments/$REF` (a receipt reload, which is what the receipt page shows on refresh).

**Expected / actual:** AC-9 says "the response is the original receipt", and R-7 says it "returns the outcome of the first confirmation". So `balanceOwed "0.00"` and "Your card balance is now £0.00", as on the first receipt; the record stores `balanceAfter 0.00`. Actually, both answer `balanceOwed "20.00"` and "… £150.50 has left your current account and paid your card. Your card balance is now £20.00 …". The settled receipt is re-rendered with the card's current balance, so its W-2 text drifts from what was first shown. A failed receipt reloaded after a later completed payment would read "Your card balance is still £0.00". Exactly-once is not affected: one Payment, one debit.
**Evidence:** 04-validation.md, AC-9 row and Bugs section. The run on 2026-11-08, head 5cdf614: 1st confirm `balanceOwed 0.00`; repeat `balanceOwed 20.00, repeated true`; GET receipt `balanceOwed 20.00`; record `balanceBefore 150.50, balanceAfter 0.00`. Hint only: `PaymentService.StoredReceiptAsync` and the Repeat path pass the current `Owed` into `BuildReceipt`, not the settlement's `BalanceAfter`. T-I-4 does not catch it because no purchase lands between its confirms.
