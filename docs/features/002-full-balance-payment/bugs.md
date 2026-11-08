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
| BUG-012 | validator | S3 | AC-9, R-7, R-12 | Repeated confirm or receipt reload after a later purchase shows the current balance, not the original receipt | plan | fix-now (option A: rule + code) | approved | plan (doc amend: PR-04 brief erratum + fix brief PR-10) → build PR-10 | | 2026-11-08 |
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
### Triage (reviewer)
**Root-cause layer: plan.** The PR-04 brief is the first artifact that states a wrong rule, and the code follows it exactly.
- **Requirements are clear and right.** R-7: a repeat "returns the outcome of the first confirmation". AC-9: "the response is the original receipt". R-12 and W-2 put the balance in the receipt text, "Your card balance is now {balance}", which is the balance at that moment of the payment. Nothing has to change.
- **The design is right but underspecified (contributing).** §7 (`.../check`: "no-op with the stored receipt when already settled") and §8 Flow 4 ("a third confirm reads the stored receipt") read as AC-9. But nothing persists a receipt, and §5 `Receipt(..., Money BalanceOwed, ...)` never says which balance the field holds. §5 `Settlement.BalanceAfter` ("balance owed immediately after", `PaymentRecords.cs:39`) is the stored value a rebuilt receipt needs. No DEC addresses it.
- **The plan is wrong.** `prs/PR-04.md:83-85`, *Receipt*: "Message = WordingV1.Receipt(state, reason, amount, **currentOwed**, ref) …, BalanceOwed = **current owed**". This is right for a receipt built at settle time, where current owed == `BalanceAfter`. It is wrong for every later rebuild of a settled payment, and it contradicts the brief's own AC-9 row (`PR-04.md:29`).
- **The code is faithful to the brief.** `PaymentService.cs:343-347` `StoredReceiptAsync` passes `GetBalanceAsync().Owed`. It serves GET (`:284`), check on a settled payment (`:244`) and the joiner fallback (`:170`). `:379` (Repeat) passes the lock's current `owed`. `:497-498` (`SettleAsync` finding the payment already settled, i.e. a check racing a confirm) passes current `before`. `BuildReceipt` (`:294-313`) puts that value into both `Message` and `BalanceOwed`. The web shows the server message verbatim (`ReceiptPage.tsx:137`), so there is no web change.
- **A test pins the current behaviour, but not as a design choice.** T-U-38 `Receipt_reload_reads_without_calling_the_bank` (`PaymentServiceTests.cs:648`) asserts `later.BalanceOwed == £10.00` after a later £10.00 purchase. That is the brief's "current owed" rule written down by the coder. The test's own Given/When/Then says "Then the stored receipt", and no DEC, design line or human decision sets it. It is green on HEAD 45a5196: `dotnet test tests/CardLab.Domain.Tests --filter …` 3/3 passed (T-U-38, T-U-12, T-U-21). T-U-12 and T-I-4 do not catch the bug because no purchase lands between confirms. T-U-21 and T-I-7 (AC-36) assert the *check's* receipt equals £20.00 = `BalanceAfter`, which agrees with the fix.
- **Which balance each receipt state should show:**
  - *Completed* or *failed*, including pending settled by a check: `Settlement.BalanceAfter`. That equals the first settled receipt on every path (`SettleAsync:523` passes `after`). A failed receipt then stays "still £150.50" after a later completed payment, instead of "still £0.00".
  - *Pending*: the card's **current** owed. There is no settlement yet. W-5 says "Your card balance **shows** {balance} until we know the outcome", a statement about the live balance. The card view's pending notice uses current owed (`CardholderViewService.cs:51`), and AC-9 covers only "a confirmation that completed". AC-10 is "same outcome" (state), and the joiner path already returns the first caller's receipt. Freezing the pending balance at `quote.Amount` (== owed at confirm) is possible, but the receipt would then disagree with the card view after an AC-36 purchase.

**Options**

| | What changes | Effort | Risk | Owned files | Resumes at | Re-tested |
|---|---|---|---|---|---|---|
| **A (recommended)** Fix the rule and the code | Brief erratum `PR-04.md:83-85`: "BalanceOwed (and W-2..W-4 `{balance}`) = `Settlement.BalanceAfter` when settled; current owed while Pending". Design §5 one-line text erratum on `Receipt.BalanceOwed`, plus the digest `:65` (text only, as with BUG-005; Gate 2 stays approved if the human agrees). Code: `BuildReceipt` uses `payment.Settlement?.BalanceAfter ?? pendingBalance` (rename the parameter, fix the doc comment). `StoredReceiptAsync` reads `GetBalanceAsync` only while Pending. This fixes all five call sites in one place. | S (≈10 prod lines, 6 tests) | Low. First-receipt paths already pass a value equal to `BalanceAfter`. Pending and the cardholder view are unchanged. `BalanceAfter` is non-null on every settled row (`SqlPaymentWriter.cs:240-253`). No schema change. | `src/CardLab.Domain/Application/PaymentService.cs`, `tests/CardLab.Domain.Tests/Application/PaymentServiceTests.cs`, `tests/CardLab.Domain.Tests/Application/PaymentStatusCheckTests.cs`, `tests/CardLab.Integration.Tests/Payments/IdempotencyTests.cs` | plan (orchestrator doc-only amend: erratum + `prs/PR-10-fix-BUG-012.md`) → build PR-10 (`pr-light`) | AC-7, AC-9, AC-10, AC-12, AC-13, AC-35, AC-36, plus the validator's BUG-012 repro |
| B. Persist the first receipt | Add a stored receipt snapshot (`Payment.ReceiptMessage`/`ReceiptBalanceOwed` columns or a table), written at settle and served on every re-read | M | Medium: a migration on an approved §6 schema, two stores and the contract test, and a second source of truth beside `BalanceAfter`. Pending still needs a live rebuild. Only gain: byte-exact text if WordingV1 ever changes. | PR-05 store files, migration, `InMemoryCardStore.cs`, `PaymentStoreContractTests.cs`, `PaymentService.cs` + tests | design (§6 data model) → plan → build | as A, plus contract P-* on SQL |
| C. Change the requirement | AC-9 "original receipt except that the balance is the card's current balance". Re-word W-2 so it does not say "is now" on a re-read | S (docs) + wording | High for the user: a paid-in-full receipt that says "balance is now £20.00". Reopens Gate 1 and the compliance read (OB-7 "properly informed"), then Gates 2 and 3. | `01-requirements.md` §17, `WordingV1`, tests | requirements | OB-7 compliance re-read, AC-7/AC-9 |
| D. Defer / won't fix | Nothing | none | see cost | none | none | none |

**Cost of not fixing (D):** every receipt reload, bookmark or retried confirm after any later card purchase shows "£150.50 … paid your card. Your card balance is now £20.00". A failed receipt reloaded after a later completed payment reads "still £0.00", which looks as if the failed payment cleared the card. Likelihood: high in real use (the card is used daily, and the receipt URL is the reload target, DEC-11); low in today's demo. Blast radius: display only, with no money, record or exactly-once impact, but it weakens OB-7. AC-9 stays ⚠️, so delivery stays "with caveats", and T-U-38 keeps pinning the wrong rule.

**Regression tests for A (write first; each RED on 45a5196 unless noted):**
1. T-U-38 amended (`PaymentServiceTests.cs:648`). Replace the £10.00 assertion with `Assert.Equal(original, later.Value)`, `Assert.Equal(Money.Zero, later.Value!.BalanceOwed)` and `Assert.Contains("Your card balance is now £0.00.", later.Value.Message)`.
2. `Repeated_confirm_after_a_later_purchase_returns_the_original_receipt` (PaymentServiceTests). Steps: confirm, `AddPurchaseAsync("20.00")`, `ConfirmAsync`. Then `Assert.Equal(original with { Repeated = true }, repeat.Value)` and `BalanceOwed == Money.Zero`.
3. `Failed_receipt_reload_after_a_later_purchase_keeps_its_balance` (PaymentServiceTests). Steps: `NextDebit(InsufficientFunds)` (W-3 £150.50), `AddPurchaseAsync("20.00")`. Then `GetReceiptAsync` and `CheckStatusAsync` both equal the original, `BalanceOwed == £150.50`, and the message contains "still £150.50".
4. `Receipt_of_a_payment_settled_by_check_keeps_the_settlement_balance` (PaymentStatusCheckTests). Arrange as T-U-21 (pending, +£20.00, check → £20.00), then `AddPurchaseAsync("5.00")`. Then `GetReceiptAsync` and a second `CheckStatusAsync` equal the check's receipt, with `BalanceOwed == £20.00`, not £25.00.
5. `Pending_receipt_shows_the_current_balance` (PaymentStatusCheckTests). Steps: pending, `AddPurchaseAsync("20.00")`, `GetReceiptAsync`. Then Pending, `BalanceOwed == £170.50`, and the message contains "Your card balance shows £170.50". This pins the pending rule on purpose. It is green before and after the fix.
6. `Repeat_and_reload_after_a_later_purchase_return_the_original_receipt` (IdempotencyTests, T-I-4b), the validator's repro over HTTP. Steps: first confirm, `SimulatePurchaseAsync(20.00)`. Then `Assert.Equal(first.Body with { Repeated = true }, repeat.Body)`, `Assert.Equal(first.Body, reload.Body)` and `balanceOwed == "0.00"`.

**Mutations (each must fail after the fix):**
- M1: `BuildReceipt` ignores `Settlement` and uses the passed balance → 1, 2, 3, 4 and 6 red.
- M2: `?? quote.Amount` instead of the pending balance → 5 red.
- M3: `BalanceBefore` instead of `BalanceAfter` → 1, 2, 4 and 6 red.

**Checks:** `dotnet test tests/CardLab.Domain.Tests` and `dotnet test tests/CardLab.Integration.Tests --filter "FullyQualifiedName~IdempotencyTests|FullyQualifiedName~StatusCheckTests"`.

**Recommendation: A.** The value the receipt needs (`BalanceAfter`) is already stored for every settled payment, so one small PR fixes the brief rule and all five call sites without touching the schema or a gate.

### Decision (human, 2026-11-08)
"A. Fix rule + code (Recommended)". Design §5 and the digest now define `Receipt.BalanceOwed` (text erratum; Gate 2 stays approved). Next: plan amend (PR-04 brief erratum + `prs/PR-10-fix-BUG-012.md`), then build PR-10 (pr-light), then re-validate AC-9 and the receipt ACs.
