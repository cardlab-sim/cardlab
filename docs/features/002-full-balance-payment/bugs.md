# Bug & fix ledger: Full balance payment (CL-3, slice 1)

<!-- Append-only. Never delete a row; change Status. The SessionStart hook reminds you of every
row whose Status is new, triaged, approved, in-progress or deferred. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-02, F3) | S3 | AC-3 | Store contract P-1 expects the test's own linked account id, which the shared SQL test database cannot return | plan | fix-now (option A) | verified | build (PR-02 fix round) | | 2026-11-07 |

| BUG-002 | reviewer (PR-04, F4) | S3 | NFR-2, R-9 | Client disconnect cancels the bank call (budget linked to request token), contrary to design §13 | design | fix-now (option A: code matches design) | fixed | build (PR-04 fix round) | | 2026-11-07 |
| BUG-003 | reviewer (PR-04, F3) | S4 | R-9 | Status check awaiting an in-flight confirm is undocumented and unbounded | design | fix-now (option A: record + cap at 10 s) | fixed | build (PR-04 fix round) | | 2026-11-07 |
| BUG-004 | reviewer (PR-05, F4) | S2 | R-15 | Steered "time out, not received" failure is not marked simulated | design | fix-now (option A: amend design + code) | fixed | build (PR-05 fix round) | | 2026-11-07 |
| BUG-005 | reviewer (PR-05, F8) | S4 | DEC-15 | Origin stored as enum name, design text said deploy_smoke | design | fix-now (amend design text) | fixed | — | | 2026-11-07 |
| BUG-006 | orchestrator (feature CI run 42410267102) | S4 | AC-10 | Disconnect test flaky: in-flight registry entry removed after the confirm returns | code | fix-now (option A) | in-progress | build (PR-04f, wave 3) | | 2026-11-07 |
| BUG-007 | reviewer (PR-07, F5) | S4 | R-1 | Shared web FakeApiClient doesn't feed simulated purchases into activity/balance; PR-07 test-local ActivityFake duplicates it with inconsistent data | test-harness | fix-now (small fix PR) | approved | build (PR-03f, wave 4) | | 2026-11-07 |
| BUG-008 | reviewer (PR-06, F4) | S4 | AC-9, AC-10 | Repeated/parallel confirm receipt time differs by < 1 ms on the wire (full-tick vs stored ms) | code | fix-now (fold into PR-04f) | in-progress | build (PR-04f) | | 2026-11-07 |
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

## BUG-006
**Repro:** feature-branch CI `backend` (run 42410267102, head d32241c): `PaymentServiceTests.Client_disconnect_never_cancels_the_debit_and_the_payment_settles` fails at line 382 (`Assert.False(f.InFlight.TryGet(...))`); passes locally.
**Expected / actual:** the registry is clear once the confirm returns; actually `InFlightPayments.RunAsync` removes the entry in an asynchronous continuation after `ConfirmAsync` has already returned.
**Evidence:** `PaymentService.cs:185` `completion.SetResult(result)` then return; `InFlightPayments.RunAsync` `await start()` → `Remove` (continuation, `RunContinuationsAsynchronously`).
### Triage (orchestrator)
code (PR-04): race in the test-visible registry cleanup; production impact nil (a check finding the stale entry awaits a completed task, then re-reads state).
### Decision (human, 2026-11-07)
"A. Small fix PR now (Recommended)": brief prs/PR-04-fix-BUG-006.md, wave 3.
### Resolution
…

## BUG-007
**Repro:** `web/src/pages/CardDetail.test.tsx:604-638` (`ActivityFake`, `paymentsOverride`) subclasses the shared `FakeApiClient` (`web/src/api/fakeApiClient.ts`, PR-03) because `simulatePurchase` never reaches `listActivity` or the balance and `origin` is hard-coded.
**Expected / actual:** one consistent shared fake; actually T-W-8 asserts "Balance owed £20.00" beside £32.50 of approved purchases (`:462`), rows concatenated, not time-ordered.
**Evidence:** reviews/PR-07.review.md F5.
### Triage (reviewer)
test-harness: follow-up change to the shared fake (not owned by PR-07); then drop `ActivityFake`.
### Decision (human, 2026-11-07)
"Small fix PR now (Recommended)": make the shared FakeApiClient consistent and delete PR-07's ActivityFake.
### Resolution
…

## BUG-008
**Repro:** first confirm's receipt `Time` carries the clock's (or fake bank's) full-tick instant; repeats and reads return the stored datetime2(3) value; T-I-4 needs a 1 ms tolerance; parallel confirms differed in ~1/19 runs.
**Expected / actual:** AC-9/AC-10 "the same outcome": identical receipts on the wire; actually sub-ms difference (screen shows minutes, unaffected).
**Evidence:** reviews/PR-06.review.md F4.
### Triage (reviewer)
code: truncate instants to ms at the source in PaymentService (`now` in ValidateAsync/SettleAsync, the bank's debit time).
### Decision (human, 2026-11-07)
"Fold into PR-04f (Recommended)"; then T-I-4 and T-I-5 assert exact equality.
### Resolution
…
