# Bug & fix ledger: Card controls: freeze/unfreeze and per-category spend limits

<!-- Append-only. Never delete a row; change Status. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-03) | S3 | AC-27, AC-34 | Deploy health wait trusts a configured commit, not the deployed build | design | fix-now (option A) | fixed | build (PR-03 fix round) | | 2026-11-05 |
| BUG-002 | coder (PR-06), reproduced by orchestrator | S2 | AC-34, R-9 | Host tests throw DirectoryNotFoundException for wwwroot after a web build | plan (skeleton PR-00) | fix-now (skeleton delta PR-00b) | fixed | build (PR-00b before wave 3) | | 2026-11-05 |
| BUG-003 | reviewer (PR-07) | S3 | AC-23 | FakeApiClient writes audit values in a format the API never returns | test-harness | fix-now (fold into PR-07 fix round) | fixed | build (PR-07 fix round) | | 2026-11-05 |
| BUG-004 | reviewer (PR-04) | S3 | AC-20 | Timestamps outside the calendar range or in non-ISO text pass validation | design | fix-now (PR-04 fix round) | fixed | build (PR-04 fix round) | | 2026-11-05 |
| BUG-005 | reviewer (PR-08) | S2 | NFR-1, AC-24 | A 16-digit merchant name is stored and returned in history | design | fix-now (option A, PR-08 fix round) | approved | build (PR-08 fix round) | | 2026-11-05 |
| BUG-006 | reviewer (PR-08) | S3 | AC-20 | Non-binding JSON body gives 500 (Development) / empty 400 instead of ProblemDetails | plan | fix-now (PR-09 fix round) | approved | build (PR-09 fix round) | | 2026-11-05 |

<!-- Severity: S1 blocker · S2 major · S3 minor · S4 cosmetic
     Root-cause layer: requirements | design | plan | code | test-harness | environment | needs-debugger
     Decision: fix-now (option A/B) | defer | wont-fix
     Status: new → triaged → approved → in-progress → fixed → verified   |  deferred  |  wont-fix -->

## BUG-001
**Repro:** in `deploy.yml`, `CardLab__Commit=<sha>` is set as an app setting; the health endpoint reports `CardLab:Commit` first. The deploy's health wait (commit == SHA) then passes even if the package step is skipped or the old binaries keep serving.
**Expected / actual:** the health wait must prove the new build is running (AC-27) / it only proves the app setting was written.
**Evidence:** reviews/PR-03.review.md findings B-1 and M-1 (`upstream:design`); design §11 row `CardLab:Commit` ("set by the deploy workflow as an app setting").
### Triage (reviewer)
Root cause in design §11: the commit identity on `demo` comes from configuration rather than the build. Option A: on `demo` the deploy workflow does not set `CardLab__Commit`; the publish stamps `-p:SourceRevisionId=$GITHUB_SHA` and health falls back to the assembly informational version (already the design §12 fallback, so PR-09's brief is unchanged). Option B: keep the app setting but move it after the package step (B-1 only; still config, not identity).
### Decision (orchestrator, 2026-11-05)
Option A, because it uses the fallback the design already specifies, changes only deploy.yml and the §11 wording, and makes the health wait prove the build. Taken by the orchestrator under the human's standing instruction: "make all decisions in requirements and tech design including as recommended". Recorded as a design erratum (no Gate 2 reopen).
### Resolution
PR-03 fix round: deploy.yml stops setting `CardLab__Commit`; settings step after the package step; azure-setup.md says `CardLab__Commit` must stay unset on demo. Verification: see reviews/PR-03.review.md *Fixes verified*.

Fixed in PR-03 (#4, squash 114bc15); checks in reviews/PR-03.review.md *Fixes verified*. Verified status waits for the validator's deploy evidence (T-C-2).

## BUG-002
**Repro:** in any worktree, `npm run build --prefix web` (part of the `build` gate), then `dotnet test tests/CardLab.Integration.Tests` → every `WebApplicationFactory` test (e.g. SeederTests) fails with `System.IO.DirectoryNotFoundException: ...\src\CardLab.Api\wwwroot\`. Deleting `web/dist` and `src/CardLab.Api/obj|bin` makes them pass (orchestrator: 2 failed → 14 passed).
**Expected / actual:** host tests pass regardless of whether the SPA was built / they fail once `web/dist/index.html` exists.
**Evidence:** `src/CardLab.Api/CardLab.Api.csproj` lines 12-14: conditional `Content Include="..\..\web\dist\**\*" LinkBase="wwwroot"`, while no `src/CardLab.Api/wwwroot/` directory exists in the content root.
### Triage (orchestrator assessment pass)
Root cause in the PR-00 skeleton wiring (static web assets/content root expects a physical `wwwroot`). Visible from code and repro, so no debugger. Option A: skeleton delta PR-00b that makes the host start with or without `web/dist` (e.g. a committed `src/CardLab.Api/wwwroot/.gitkeep`, or adjusting the item/WebRoot wiring) plus a regression proof (build web, then the host tests pass). Option B: forbid web builds before integration runs (fragile; the CI e2e job publishes with the SPA). Fix-worth: high (blocks PR-08/09/10 host tests and CI), effort small.
### Decision (orchestrator, 2026-11-05)
Option A as PR-00b before wave 3. Orchestrator under the human's standing instruction: "make all decisions in requirements and tech design including as recommended" (autopilot: smallest blast radius, fix in code).
### Resolution
PR-00b (#11, squash 1da512e): committed `src/CardLab.Api/wwwroot/.gitkeep` so a physical web root always exists. Coder repro on the PR-06 tests: before, SeederTests 2 failed with `DirectoryNotFoundException`; after, 2 passed with and without `web/dist`; publish still ships `wwwroot/index.html`. Orchestrator re-checks during PR-08/09 host runs.

## BUG-003
**Repro:** `web/src/api/fakeApiClient.ts` (PR-02) records audit `oldValue/newValue` as `"50.00"` and `"active"`; design §6 and PR-05/PR-09 return `"Fuel/Day/50.00"` and `"Active"`/`"Frozen"`. PR-07's audit list formats with `formatMoney`, which throws on the real format (review B1).
**Expected / actual:** the fake matches the API contract / it does not, so the UI crash passed unit tests.
**Evidence:** reviews/PR-07.review.md B1 and U1.
### Triage (reviewer)
Test-harness layer; brief PR-07 also lacked the §6 value format. Fix: fake emits `${PascalCategory}/${PascalPeriod}/${amount}` and `Active`/`Frozen`; PR-07's formatter never throws.
### Decision (orchestrator, 2026-11-05)
Fix now inside PR-07's fix round: `web/src/api/fakeApiClient.ts` and `web/src/api/fakeApiClient.test.ts` released to PR-07 (PR-02 is merged; no other open PR owns them). Orchestrator under the human's standing instruction.
### Resolution
PR-07 fix round (#7): fake emits the §6 format, audit formatter never throws, new API-format audit test. Verified via reviews/PR-07.review.md *Fixes verified*; E2E (PR-10) will exercise the real API.

## BUG-004
**Repro:** `POST /api/authorisations` with `timestamp: "9999-12-31T12:00:00Z"` passes `TransactionRules.Validate` and `PeriodCalendar.RangeContaining` throws `ArgumentOutOfRangeException` (would be a 500, not `invalid_request`); `"04/11/2026 10:00:00 +00:00"` is accepted and read as 11 April.
**Expected / actual:** malformed or out-of-range timestamps decline with `invalid_request` (AC-20) / they crash or are misread.
**Evidence:** reviews/PR-04.review.md F-1 and F-5 (`upstream:design`).
### Triage (reviewer)
Design §9 lists "bad timestamp" without defining it. Option A: timestamps must be strict ISO-8601 with an explicit offset (`yyyy-MM-ddTHH:mm:ss[.fff](Z|±hh:mm)`) and fall in [2000-01-01T00:00Z, 2100-01-01T00:00Z); anything else is a `timestamp` problem.
### Decision (orchestrator, 2026-11-05)
Option A (the reviewer's recommendation), recorded as a design erratum for §9; no Gate 2 reopen. Orchestrator under the human's standing instruction: "make all decisions in requirements and tech design including as recommended".
### Resolution
PR-04 fix round (#9): strict ISO-8601 shape + [2000, 2100) window in Validate and ValidatePurchase, with rejection and boundary cases; checks in reviews/PR-04.review.md *Fixes verified*.

## BUG-005
**Repro:** `POST /api/authorisations` with a valid request whose `merchant` is `"9999000100000001"` → stored, and `GET /api/cards/{id}/transactions` returns it.
**Expected / actual:** no 16-digit run is ever stored, logged or displayed (NFR-1, AC-24) / it is stored and displayed.
**Evidence:** reviews/PR-08.review.md m-3 (`upstream:design`); PR-11's T-I-7 would fail on it.
### Triage (reviewer)
Option A: `TransactionRules` rejects a merchant containing a run of 13 or more consecutive digits (any PAN-like run, with or without spaces/dashes between groups) as a `merchant` problem → `invalid_request`, nothing stored. Option B: accept the risk in design §10 and narrow T-I-7.
### Decision (orchestrator, 2026-11-05)
Option A, because option B would break the approved NFR-1 ("no card numbers anywhere"); A is the smallest change that keeps it. Design erratum for §9/§10; no Gate 2 reopen. `TransactionRules.cs` and `TransactionRulesTests.cs` (PR-04, merged) released to PR-08's fix round. Orchestrator under the human's standing instruction: "make all decisions in requirements and tech design including as recommended".
### Resolution
PR-08 fix round.

## BUG-006
**Repro:** `POST /api/authorisations` with `{"cardNumber": 9999000100000001, ...}` (number, not string) → 500 on the Development host, empty-body 400 in Production; the admin limit PUT has the same gap.
**Expected / actual:** 400 `application/problem+json` without echoing input / 500 or empty 400.
**Evidence:** reviews/PR-08.review.md M-1 (`upstream:plan`): PR-08's planner decision assumed framework ProblemDetails for binding failures.
### Triage (reviewer)
Fix: `ThrowOnBadRequest = true` (route handler options) in `Program.cs` and map `BadHttpRequestException` to its status code as ProblemDetails in `CardLabExceptionHandler`, never copying the exception message.
### Decision (orchestrator, 2026-11-05)
Fix in PR-09's fix round (PR-09 owns `CardLabExceptionHandler`; `src/CardLab.Api/Program.cs`, owned by merged PR-00b, released to PR-09). Orchestrator under the human's standing instruction.
### Resolution
PR-09 fix round.
