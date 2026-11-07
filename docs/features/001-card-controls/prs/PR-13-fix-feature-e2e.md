---
pr: PR-13
feature: 001-card-controls
title: Feature review fixes: keyboard simulate, Firefox, CI commit chain, README (BUG-009)
wave: 5
depends_on: [PR-10, PR-11]
branch: pr/001-card-controls/13-fix-feature-e2e
base: feat/001-card-controls
---

# PR-13: Feature review fixes: keyboard simulate, Firefox, CI commit chain, README (BUG-009)

Fix brief written by the orchestrator from `reviews/feature.review.md` findings F-2, F-5, F-7, F-10 and F-12. Each finding in that file has an exact fix and a check; read those rows (they are part of this brief).

## Goal
Prove every action (including simulate purchase) by keyboard, cover Firefox as NFR-6 requires (BUG-009), exercise the BUG-001 commit chain in CI before any deploy, remove a dead stub, and document how to run CardLab locally.

## Acceptance criteria covered (verbatim from requirements)
| AC | Given | When | Then |
|----|-------|------|------|
| AC-37 | every page of the admin UI | the end-to-end suite runs its automated accessibility check and drives each action by keyboard only | zero critical or serious violations are reported and every journey completes |
| AC-27 | a merge to main with green CI | the deploy workflow runs | the Azure environment serves the new build, identifiable by commit on its health endpoint |

## Design context (excerpts)
- NFR-6: latest desktop Chrome, Edge and Firefox. BUG-009 decision: add a `local-firefox` Playwright project (`devices['Desktop Firefox']`, `testIgnore: ['smoke/**']`), run CI e2e with `--project local --project local-firefox` and install `chromium firefox`; Edge is covered by the shared Chromium engine (design erratum).
- BUG-001 chain: publish stamps `-p:SourceRevisionId=<sha>`; health reports the part after the last '+'; the e2e job must publish that way and must NOT set `CardLab__Commit`, then assert `GET /api/health` reports `github.sha`.
- AC-37 journeys: J-1 freeze/unfreeze, J-2 limits, J-3 simulate purchase. The simulate form is a native `<form>`; "Amount" and "Category" appear twice on the card page (limits form first, simulate form second in DOM order).

## Files you own (edit only these)
| File | Status | What to do |
|------|--------|------------|
| tests/e2e/src/a11y.spec.ts | merged | F-2 extend the keyboard-only test through simulate purchase; rename to "J-1, J-2 and J-3 complete with the keyboard only" |
| tests/e2e/playwright.config.ts | merged | F-7 add `local-firefox` project |
| .github/workflows/ci.yml | merged | F-5 publish with SourceRevisionId, drop `CardLab__Commit`, assert health commit == github.sha; F-7 install firefox and run both projects |
| web/src/notImplemented.ts | merged (unused) | F-10 delete |
| README.md | initial | F-12 "Run locally" section |

## Read-only dependencies (signatures you call; don't edit)
`tests/e2e/src/a11y.ts` (`tabTo`, `expectNoSeriousViolations`), `tests/e2e/src/pages/*`, `web/src/pages/cardDetail/sections.tsx`, `docs/sdlc/project.json`, `docs/ops/azure-setup.md`.

## Tests to implement (TDD order)
| # | Test id | File | Layer | Given/When/Then | Guards against |
|---|---------|------|-------|-----------------|----------------|
| 1 | T-E-5 | tests/e2e/src/a11y.spec.ts | e2e | Given the card page, When J-1, J-2 and J-3 are driven by keyboard only, Then each completes (simulate result Approved, top transaction row shows the merchant) | simulate not keyboard-operable (AC-37) |
| 2 | T-E-1..T-E-6 on Firefox | tests/e2e/playwright.config.ts | e2e | the `local` suite passes on Desktop Firefox | Firefox-only breakage (NFR-6) |
| 3 | T-C-2 (CI part) | .github/workflows/ci.yml | CI | health on the CI-published host reports `github.sha` | BUG-001 chain regression before deploy |

## Harness to use
Playwright with LocalDB locally (`npx playwright install chromium firefox` in your own worktree); the CI e2e job with the mssql service.

## Commands
- `npm run build --prefix web`
- `npm run e2e --prefix tests/e2e -- --project local --project local-firefox`
- `npm run lint --prefix web && npm run typecheck --prefix web && npm test --prefix web -- --run`
- `npm run lint --prefix tests/e2e && npm run typecheck --prefix tests/e2e`
- `actionlint .github/workflows/ci.yml`
- Scope check: `bash ${CLAUDE_PLUGIN_ROOT}/scripts/py.sh ${CLAUDE_PLUGIN_ROOT}/scripts/doc_lint.py scope docs/features/001-card-controls/prs/PR-13-fix-feature-e2e.md`

## Rules for this PR
- F-2 mutation: give the simulate submit button `tabIndex={-1}` (temporary) → the keyboard test fails with "Could not tab to"; revert.
- Push and wait for the PR's own CI: all checks green, e2e on both browsers, ZAP still FAIL 0 / WARN 0.
- Nothing against Azure. PR body without a BOM (write it from the Bash tool).
- Ticket key: PR title `CL-1 PR-13: Feature review fixes: keyboard simulate, Firefox, CI commit chain, README (BUG-009)`, body first line `Ticket: CL-1`.

## Out of scope
Backend, deploy workflow and design docs (PR-12 / orchestrator).

## Definition of done
- [ ] F-2, F-5, F-7, F-10, F-12 checks from reviews/feature.review.md pass
- [ ] Local e2e green on both projects; PR CI green
- [ ] Only owned files changed (`doc_lint.py scope` passes)
