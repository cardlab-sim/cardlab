---
pr: PR-12
feature: 001-card-controls
title: Feature review fixes: startup retry, limit order, HSTS, build version (BUG-008)
wave: 5
depends_on: [PR-11, PR-10]
branch: pr/001-card-controls/12-fix-feature-backend
base: feat/001-card-controls
---

# PR-12: Feature review fixes: startup retry, limit order, HSTS, build version (BUG-008)

Fix brief written by the orchestrator from `reviews/feature.review.md` findings F-1, F-3, F-4 and F-6. Each finding in that file has an exact fix and a check; read those rows (they are part of this brief).

## Goal
Make `demo` survive the free database's idle auto-pause (BUG-008), make the limit order match the store contract, send HSTS on `demo`, and stamp log records with the real build commit.

## Acceptance criteria covered (verbatim from requirements)
| AC | Given | When | Then |
|----|-------|------|------|
| AC-34 | the application is running | the health endpoint is called | it answers within 60 s with the build commit and a database reachability status |
| AC-36 | the deployed environment, already warmed up | the admin submits the simulate-purchase form | the outcome is shown within 2 s |

## Design context (excerpts)
- BUG-008 decision (bugs.md): retry `MigrateAsync` + `EnsureSeedAsync` on `SqlException` for up to 120 s total with a 5 s delay; log one fixed message per attempt without connection details; rethrow after the deadline. Demo connection string gains `;Connect Timeout=60;Connect Retry Count=3;Connect Retry Interval=10`.
- Store contract (`src/CardLab.Domain/Cards/CardModels.cs:22`): `CardDetail.Limits` ordered by category then period (enum order).
- Design §10: HSTS on `demo`; App Service terminates TLS, so forwarded headers must be honoured (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`).
- DEC-12: `service.version=<commit>`; on `demo` the commit is the part after the last '+' of the informational version (BUG-001 erratum). Today that logic is `HealthEndpoints.ResolveCommit`.

## Files you own (edit only these)
| File | Status | What to do |
|------|--------|------------|
| src/CardLab.Infrastructure/Sql/DatabaseSetup.cs | merged | F-1 retry loop |
| tests/CardLab.Integration.Tests/Seed/StartupRetryTests.cs | new (create) | F-1 test: database OFFLINE, start initialisation, ONLINE after 10 s, completes within 60 s |
| .github/workflows/deploy.yml | merged | F-1 connection-string retry settings; F-4 `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` in the app settings |
| src/CardLab.Infrastructure/Sql/SqlCardStore.cs | merged | F-3 order limits by category then period |
| tests/CardLab.TestKit/Fakes/InMemoryCardStore.cs | merged | F-3 same ordering |
| tests/CardLab.TestKit/Contracts/CardStoreContractTests.cs | merged | F-3 one contract case: limits come back in category-then-period order |
| src/CardLab.Infrastructure/BuildInfo.cs | new (create) | F-6 `BuildInfo.Commit(IConfiguration)`: configured value, else part after last '+' of the entry assembly informational version, else "unknown" |
| src/CardLab.Infrastructure/Logging/Telemetry.cs | merged | F-6 `service.version` from `BuildInfo.Commit` |
| src/CardLab.Api/Endpoints/HealthEndpoints.cs | merged | F-6 call `BuildInfo` (keep a `ResolveCommit` wrapper or move it; health behaviour unchanged) |
| tests/CardLab.Integration.Tests/Api/HealthTests.cs | merged | F-6 keep the T-I-10 commit theory, pointed at the moved method |

## Read-only dependencies (signatures you call; don't edit)
`src/CardLab.Domain/Cards/CardModels.cs`, `src/CardLab.Infrastructure/DependencyInjection.cs`, `src/CardLab.Api/Program.cs`, `tests/CardLab.Integration.Tests/Harness/*`.

## Tests to implement (TDD order)
| # | Test id | File | Layer | Given/When/Then | Guards against |
|---|---------|------|-------|-----------------|----------------|
| 1 | T-I-22 | tests/CardLab.Integration.Tests/Seed/StartupRetryTests.cs | integration | Given the test database OFFLINE, When `InitialiseDatabaseAsync` starts and the database goes ONLINE after 10 s, Then it completes without exception within 60 s | crash loop on database resume (BUG-008) |
| 2 | T-U-21 / T-I-12 | tests/CardLab.TestKit/Contracts/CardStoreContractTests.cs | contract | Given limits upserted out of order, When GetCardAsync, Then limits are ordered by category then period | random limit order (F-3) |
| 3 | T-I-10 | tests/CardLab.Integration.Tests/Api/HealthTests.cs | integration | existing commit theory against the moved resolver | commit format regression (BUG-001) |

## Harness to use
LocalDB (`(localdb)\MSSQLLocalDB`). `SqlTestDatabase` for an isolated database to take OFFLINE/ONLINE (use `ALTER DATABASE ... SET OFFLINE WITH ROLLBACK IMMEDIATE` on a separate master connection).

## Commands
- `dotnet build CardLab.sln -warnaserror`
- `dotnet test tests/CardLab.Domain.Tests && npm test --prefix web -- --run`
- `dotnet test tests/CardLab.Integration.Tests`
- `dotnet format CardLab.sln --verify-no-changes`
- `actionlint .github/workflows/deploy.yml` (if available)
- Scope check: `bash ${CLAUDE_PLUGIN_ROOT}/scripts/py.sh ${CLAUDE_PLUGIN_ROOT}/scripts/doc_lint.py scope docs/features/001-card-controls/prs/PR-12-fix-feature-backend.md`

## Rules for this PR
- Red first for tests 1 and 2; prove each with the mutation in the review row (remove the retry loop; remove the ordering).
- No change to API behaviour beyond the ordering; no secrets; nothing run against Azure.
- Ticket key: PR title `CL-1 PR-12: Feature review fixes: startup retry, limit order, HSTS, build version (BUG-008)`, body first line `Ticket: CL-1`.

## Out of scope
E2E, CI and README changes (PR-13); design docs (orchestrator).

## Definition of done
- [ ] F-1, F-3, F-4, F-6 checks from reviews/feature.review.md pass
- [ ] Gate green: build, unit, lint, typecheck, integration
- [ ] Only owned files changed (`doc_lint.py scope` passes)
