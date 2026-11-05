# Bug & fix ledger: Card controls: freeze/unfreeze and per-category spend limits

<!-- Append-only. Never delete a row; change Status. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-03) | S3 | AC-27, AC-34 | Deploy health wait trusts a configured commit, not the deployed build | design | fix-now (option A) | fixed | build (PR-03 fix round) | | 2026-11-05 |
| BUG-002 | coder (PR-06), reproduced by orchestrator | S2 | AC-34, R-9 | Host tests throw DirectoryNotFoundException for wwwroot after a web build | plan (skeleton PR-00) | fix-now (skeleton delta PR-00b) | fixed | build (PR-00b before wave 3) | | 2026-11-05 |
| BUG-003 | reviewer (PR-07) | S3 | AC-23 | FakeApiClient writes audit values in a format the API never returns | test-harness | fix-now (fold into PR-07 fix round) | approved | build (PR-07 fix round) | | 2026-11-05 |

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
PR-07 fix round.
