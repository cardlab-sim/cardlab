# Bug & fix ledger: Card controls: freeze/unfreeze and per-category spend limits

<!-- Append-only. Never delete a row; change Status. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-03) | S3 | AC-27, AC-34 | Deploy health wait trusts a configured commit, not the deployed build | design | fix-now (option A) | fixed | build (PR-03 fix round) | | 2026-11-05 |

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
