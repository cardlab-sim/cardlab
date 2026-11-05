---
pr: PR-00b
feature: 001-card-controls
title: Host starts with or without the built SPA (BUG-002)
wave: 2
depends_on: [PR-00]
branch: pr/001-card-controls/00b-wwwroot
base: feat/001-card-controls
---

# PR-00b: Host starts with or without the built SPA (BUG-002)

Skeleton delta written by the orchestrator (no planner). Lands before wave 3 so PR-08, PR-09 and PR-10 can run host tests after a web build.

## Goal
Make every `WebApplicationFactory` host test (and `dotnet run`) work whether or not `web/dist` exists, while `dotnet publish` still copies the Vite build into `wwwroot/` (DEC-3: one host serves API + SPA, `MapFallbackToFile("index.html")`).

## Acceptance criteria covered (verbatim from requirements)
| AC | Given | When | Then |
|----|-------|------|------|
| AC-34 | the application is running | the health endpoint is called | it answers within 60 s with the build commit and a database reachability status |

(Here: the host must start in tests at all; R-9 CI integration and e2e jobs depend on it.)

## Design context (excerpts)
- DEC-3: the SPA is served from `wwwroot/` of `src/CardLab.Api`; the Vite build (`web/dist`) is copied there at publish. `MapFallbackToFile("index.html")` serves SPA routes.
- Bug evidence: `src/CardLab.Api/CardLab.Api.csproj` lines 12-14 add `Content Include="..\..\web\dist\**\*" LinkBase="wwwroot"` only when `web\dist\index.html` exists. After `npm run build --prefix web`, integration tests using `WebApplicationFactory<Program>` fail with `System.IO.DirectoryNotFoundException: ...\src\CardLab.Api\wwwroot\`; with `web/dist` and `src/CardLab.Api/obj|bin` deleted they pass.

## Files you own (edit only these)
| File | Status in skeleton | What to do |
|------|--------------------|------------|
| src/CardLab.Api/CardLab.Api.csproj | skeleton | fix the SPA content wiring so a missing physical `wwwroot` cannot break the host |
| src/CardLab.Api/wwwroot/.gitkeep | new (create) | only if your fix needs a physical `wwwroot` directory; otherwise do not create it |
| src/CardLab.Api/Program.cs | skeleton | only if the fix needs a startup change (e.g. web root handling); keep everything else as is |

## Read-only dependencies (signatures you call; don't edit)
`tests/CardLab.Integration.Tests/Harness/ApiFactory.cs`, `tests/CardLab.Integration.Tests/Seed/SeederTests.cs` (they reproduce the bug).

## Tests to implement (TDD order)
| # | Test id | File | Layer | Given/When/Then | Guards against |
|---|---------|------|-------|-----------------|----------------|
| 1 | (repro) | existing `tests/CardLab.Integration.Tests` | integration | Given `npm run build --prefix web` has run, When `dotnet test tests/CardLab.Integration.Tests`, Then no `DirectoryNotFoundException` and the suite has 0 failures | host broken by the SPA build (BUG-002) |
| 2 | (repro) | same | integration | Given `web/dist` deleted and `src/CardLab.Api/obj`, `bin` cleaned, When the same suite runs, Then 0 failures | fix depending on the SPA being built |
| 3 | (publish) | — | build | Given the web build, When `dotnet publish src/CardLab.Api -c Release -o publish`, Then `publish/wwwroot/index.html` exists | SPA no longer shipped |

No new test file: the existing host tests are the regression proof. Record the before/after output of #1 in the PR body.

## Harness to use
LocalDB (`(localdb)\MSSQLLocalDB`) for integration tests. Run `npm ci --prefix web` in your own worktree; never link the main checkout's node_modules.

## Commands
- `npm run build --prefix web`
- `dotnet build CardLab.sln -warnaserror`
- `dotnet test tests/CardLab.Domain.Tests && npm test --prefix web -- --run`
- `dotnet test tests/CardLab.Integration.Tests`
- `dotnet publish src/CardLab.Api -c Release -o publish` (delete `publish/` afterwards; it is gitignored)
- Scope check: `bash ${CLAUDE_PLUGIN_ROOT}/scripts/py.sh ${CLAUDE_PLUGIN_ROOT}/scripts/doc_lint.py scope docs/features/001-card-controls/prs/PR-00b.md`

## Rules for this PR
- Reproduce the failure first (test #1 red) and paste the error line in the PR body.
- Smallest change; no behaviour change to the API or SPA serving in production.
- Ticket key: the PR title is `CL-1 PR-00b: Host starts with or without the built SPA (BUG-002)` and the body's first line is `Ticket: CL-1`.

## Out of scope
Any endpoint, middleware or test change.

## Definition of done
- [ ] Tests #1-#3 hold, with before/after output in the PR body
- [ ] Gate green: build, unit, lint, typecheck, integration
- [ ] Only owned files changed (`doc_lint.py scope` passes)
