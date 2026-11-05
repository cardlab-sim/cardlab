---
feature: 001-card-controls
branch: feat/001-card-controls
commit: 0cccdd5cd32a9329cebb7c70bf3f9c937aaa3117
validated_at: 2026-11-05T13:00Z
verdict: DELIVERED WITH CAVEATS
---

# Validation: Card controls: freeze/unfreeze and per-category spend limits

## Verdict
**DELIVERED WITH CAVEATS.** Everything that can be proven locally or in CI passes: 36 of 40 ACs, 0 failures.
Every layer of the test pyramid is green, both locally and in CI on the head commit. My own scripted API checks
(49), exploratory browser checks (Chrome, Firefox and real Microsoft Edge), PAN scans of logs, responses, stored rows
and screens, and a p95 of 14.8 ms (budget 300 ms) all agree with the requirements.

The caveats:
1. **4 ACs are PENDING-HUMAN** (AC-27, AC-28, AC-34 on `demo`, AC-36). They can only be proven on the Azure
   `demo` environment, which does not exist yet: the one-off setup in `docs/ops/azure-setup.md` has not been done and
   `deploy.yml` has never run. G-1 ("end to end in the deployed environment") and G-3 (deploy) are therefore not yet
   demonstrated. The BUG-001 and BUG-008 live checks are pending for the same reason. See the *Human run sheet*.
2. **AC-29 passes with a caveat.** No secrets or stored credentials exist (OIDC only), but GitHub secret scanning is
   **disabled** on this public repository, so "secret scanning reports no secrets" has no scanner report behind it
   (BUG-012).
3. One cosmetic defect: a stale rollback comment in `deploy.yml` (BUG-011).

Tally: **pass 36** (35 clean + 1 with caveat: AC-29) · **fail 0** · **pending-human 4**.

## Test runs
All local runs used a fresh detached worktree at `0cccdd5` on Windows, .NET 10 SDK (10.0.403), Node and LocalDB
`(localdb)\MSSQLLocalDB`. CI evidence is PR #3 (head `0cccdd5`): ci run
https://github.com/cardlab-sim/cardlab/actions/runs/42028992142 (pull_request), ci run
https://github.com/cardlab-sim/cardlab/actions/runs/42028983852 (push), and codeql run
https://github.com/cardlab-sim/cardlab/actions/runs/42028992044. All are `success`.

| Layer | Command | Passed | Failed | Skipped/todo | Duration |
|-------|---------|--------|--------|--------------|----------|
| Install | `dotnet restore CardLab.sln && npm ci --prefix web && npm ci --prefix tests/e2e` | exit 0 | | | 12 s |
| Build | `dotnet build CardLab.sln -warnaserror && npm run build --prefix web` | 0 warnings, 0 errors | | | ~5 s; bundle 245.8 kB JS / **78.6 kB gzip** (budget ~200 kB) |
| Lint + typecheck | `dotnet format CardLab.sln --verify-no-changes && npm run lint` (web, e2e) `&& npm run typecheck` (web, e2e) | exit 0 | | | |
| Unit (domain + application + properties) | `dotnet test tests/CardLab.Domain.Tests` | 148 | 0 | 0 | 0.27 s (3.1 s wall); CI 148/0 |
| Unit (web) | `npm test --prefix web -- --run` | 58 (8 files) | 0 | 0 | 3.9 s; CI green |
| Integration (LocalDB) | `dotnet test tests/CardLab.Integration.Tests` | 66 | 0 | 0 | 38 s (42 s wall); CI (mssql 2022) 66/0 in 47 s |
| E2E (Chrome + Firefox) | `npm run e2e --prefix tests/e2e -- --project local --project local-firefox` against the published host (`BASE_URL=http://localhost:5080`) | 14 | 0 | 0 | 22.7 s; CI 14/0 in 42.7 s, no flaky retries |
| E2E smoke (local host) | `DEMO_URL=http://localhost:5080 npm run e2e --prefix tests/e2e -- --project smoke` | 1 | 0 | 0 | 1.5 s |
| Validator scratch E2E (Chrome, Firefox, **Edge 156**) | shipped suite + 5 exploratory specs, temporary uncommitted config in the scratch worktree | 36 | 0 | 0 | 1.1 min |
| Validator scripted acceptance | Python script over the HTTP API (49 checks) | 49 | 0 | | 4.7 s |
| ZAP baseline (CI) | step `ZAP baseline` in `ci / e2e` | `FAIL-NEW: 0 WARN-NEW: 0 IGNORE: 3 PASS: 65` | | | |
| CodeQL (CI) | `analyze (csharp)`, `analyze (javascript-typescript)` | pass | | | 1m50s / 47s |

The host for the local E2E and the scripted checks was published like CI publishes it:
`dotnet publish src/CardLab.Api -c Release -p:SourceRevisionId=<HEAD>`. It ran with `ASPNETCORE_ENVIRONMENT=Production`,
`CardLab__TestEndpoints=true`, the committed development lookup key, and its own database `CardLab_Validate001`
(dropped afterwards).

## Acceptance criteria
"Script" means the validator's scripted API acceptance check (see *Commands run*). "Mapped tests" come from the design
§15.3 map; for each one I checked that the assertion covers the AC's *Then*.

| AC | Proof (test id / manual script) | Result | Verdict | Notes |
|----|---------------------------------|--------|---------|-------|
| AC-1 | T-U-1 `FreezeTests`; T-I-1 `Freeze_through_the_api`; script | pass | PASS | Script: `200 {status: frozen}`. Exactly 1 audit event: `freeze`, `Sam Ellis`, `atUtc 2026-11-05T12:47:29.773+00:00`, `Active → Frozen`. Last four: the domain event carries `CardRef(Id, LastFour)` (T-U-1 asserts `0001`); the stored row joins the card for it (design §6), and the per-card read shows `lastFour: 0001`. |
| AC-2 | T-U-2; script | pass | PASS | A second freeze returns 200 `frozen`, and the audit count stays 1. |
| AC-3 | T-U-3; T-I-1 `Unfreeze_through_the_api`; script | pass | PASS | `active`; newest event `unfreeze`, `Frozen → Active`. |
| AC-4 | T-I-2 `Frozen_card_is_declined_and_stored`; script | pass | PASS | `200 declined card_frozen`, stored as declined. Spend unchanged: after unfreezing and adding a £1.00 Fuel/day limit, £1.00 is approved with remaining `0.00`. |
| AC-5 | T-I-2 `Unfrozen_card_is_approved`; script; T-E-1 | pass | PASS | `approved` after freeze → unfreeze. |
| AC-6 | T-U-4; T-I-3 `Set_change_remove_limit_through_the_api`; T-E-2; script | pass | PASS | Limits `[fuel/day/50.00]`; audit `limit_set`, new `Fuel/Day/50.00`. |
| AC-7 | T-U-5; T-I-3; T-E-2; script | pass | PASS | Exactly one Fuel/day limit, `80.00`; `limit_changed` `Fuel/Day/50.00 → Fuel/Day/80.00`. |
| AC-8 | T-U-6; T-I-3; T-E-2; script | pass | PASS | `204`; no limit; `limit_removed`, old `Fuel/Day/80.00`. |
| AC-9 | T-U-7; T-I-3 `Invalid_limits_are_400_with_no_side_effects` (7 cases); T-E-2; script; X-5 (UI) | pass | PASS | `-1.00`, `1000000.01` and `10.005` → 400, both on an existing limit and on a new category. The limit is unchanged and the audit count unchanged. The bounds `0.00` and `1000000.00` are accepted. |
| AC-10 | T-U-8; T-I-4 `Limit_arithmetic_end_to_end`; script | pass | PASS | £30 + £20 → `approved`, remaining `0.00`. |
| AC-11 | T-U-9; T-I-4; script | pass | PASS | £20.01 → `200 declined limit_exceeded`, stored declined, remaining `20.00`. |
| AC-12 | T-U-10; T-I-4; script | pass | PASS | £20.00 after the decline → `approved`. |
| AC-13 | T-U-11 `PeriodCalendarTests`; script | pass | PASS | £10 at `2026-10-24T23:30:00Z` → approved. Control: `22:59:59Z` (same London day) → `limit_exceeded`. |
| AC-14 | T-U-12; script | pass | PASS | Monday `2026-11-09T00:30Z` → approved. Control: Sunday `23:59:59Z` → `limit_exceeded`. |
| AC-15 | T-U-13; script | pass | PASS | `2026-12-01T00:00Z` → approved. Control: `2026-11-30T23:59:59Z` → `limit_exceeded`. |
| AC-16 | T-U-14; script | pass | PASS | 4 × £45 this month, then £30 → `limit_exceeded`. Both limits are reported: day remaining `50.00`, month remaining `20.00`. |
| AC-17 | T-U-15; script | pass | PASS | £0.00 limit, £0.01 → `limit_exceeded`. |
| AC-18 | T-U-16; script | pass | PASS | £5,000 Travel with only a Fuel limit → `approved`, `limits: []`. |
| AC-19 | T-I-5 `Unknown_card_is_404_and_stores_nothing`; T-E-4; script | pass | PASS | `404 declined card_not_found`, `transactionId: null`. Transaction counts on all three cards are unchanged. |
| AC-20 | T-U-17 `TransactionRulesTests` (15); T-I-5 `Malformed_requests_are_400_and_store_nothing`; script | pass | PASS | 15 malformed variants → `400 invalid_request`, `transactionId: null`, nothing stored: missing amount, `0.00`, unknown category, `yesterday`, no offset, year 9999, `1000000.01`, 3 dp, negative, `1e2`, 15-digit card, spaced card, blank merchant, 201-char merchant, PAN merchant. A non-JSON or empty body, or a numeric card number, gives a 400 ProblemDetails that does not echo the input. |
| AC-21 | T-I-6 `Ten_parallel_requests_approve_exactly_five`; script (10 threads released by a barrier) | pass | PASS | 5 × `approved`, 5 × `limit_exceeded`; approved sum £50.00; a later £0.01 shows remaining `0.00`. |
| AC-22 | T-E-3 `simulate.spec.ts` (Chrome, Firefox, Edge); script | pass | PASS | The notice shows the outcome and the reason code (`approved`, `limit_exceeded`, `£0.00 remaining`), and the new row is at the top. API: the simulate `transactionId` equals the top history row. |
| AC-23 | T-E-2 `limits.spec.ts`; script | pass | PASS | The UI shows 3 events newest first, each with action, `Sam Ellis`, a London time (`d Mon yyyy HH:mm`) and old/new values. The API order is `limit_removed`, `limit_changed`, `limit_set`, with descending `atUtc`. |
| AC-24 | T-I-7 `NoPanTests`; T-E-4 `privacy.spec.ts`; validator scans | pass | PASS | The validator scanned 58,745 host-log lines (console OTel exporter, all journeys including unknown-card and PAN-merchant requests), 2,910 lines of API responses, a full dump of every DB table, and on-screen text (X-5). There were **0** occurrences of any seeded or probed card number, spaced or unspaced. The only 16-digit runs in the log are OTel trace/span/parent ids (hex, e.g. `ParentId: 0000000000000000`), not card numbers. |
| AC-25 | T-P-1..T-P-5 `LimitProperties`, `PeriodProperties`; run in CI (`backend` unit 148/0) | pass | PASS | Read: T-P-1 checks against an independent decimal oracle (never exceeds; exact fill approved). T-P-2: declines don't change spend. T-P-3: remaining = max(0, limit − spend). T-P-4: every instant falls in exactly one day, week and month (tiling, London midnights; generator biased to 2026-03-29 / 2026-10-25). |
| AC-26 | CI checks on PR #16 (base `feat/001-card-controls`) and on PR #3; ticket-key step replayed | pass | PASS | Reported checks: `backend` (build + unit + integration), `web`, `e2e` (Playwright + ZAP), `ticket-key`, `analyze (csharp)`, `analyze (javascript-typescript)`, `CodeQL`. Rejection: the exact `run:` script was extracted from `ci.yml` and run with the default pattern `CL-\d+` (no `TICKET_KEY_PATTERN` variable is set) under a UTF-8 locale like the runner. Title `Fix typo` + body `No ticket here` → exit 1 with `::error::PR title does not contain a ticket key…` and `…body…`. Key in only one of title/body → exit 1. `CL-1` in both → exit 0. No real PR ever lacked the key (16/16 have `CL-1`; 74 ticket-key jobs succeeded, 5 were cancelled), so no failing run exists to cite. |
| AC-27 | T-C-2 (deploy evidence) | not run | PENDING-HUMAN | `demo` does not exist and `deploy.yml` has never run. Prepared locally: the published host reports `commit = 0cccdd5…` from `SourceRevisionId` (the BUG-001 chain), and CI step "Health reports the published commit (BUG-001 chain)" passes. Run sheet R-3/R-4. |
| AC-28 | T-C-3 (resource evidence) | not run | PENDING-HUMAN | Static evidence: `infra/main.bicep` declares an F1 / `Free` Linux plan (`alwaysOn: false`) and Azure SQL `GP_S_Gen5` capacity 1 with `useFreeLimit: true` and `freeLimitExhaustionBehavior: AutoPause`; there are no other resources. Run sheet R-5. |
| AC-29 | T-C-4; workflow grep; history scan; repo settings | pass (caveat) | PASS (caveat) | `deploy.yml` uses `azure/login@v2` with `client-id/tenant-id/subscription-id` from `vars.*` and `permissions: id-token: write`; there is no `secrets.` reference in any workflow. `gh secret list` is empty. A regex scan of all 130 commits (cloud keys, tokens, private keys, SAS, client secrets, passwords, JWTs) found no secret; the only hit is a doc line with a `<job-generated>` placeholder. The committed development lookup key is documented as non-secret (DEC-8). **Caveat:** GitHub secret scanning and push protection are **disabled** on this public repo (`security_and_analysis.secret_scanning.status = disabled`), so no scanner report exists (BUG-012). Run sheet R-1. |
| AC-30 | T-U-18; T-I-8 `Lowering_a_limit_below_spend_declines_further_spend`; script | pass | PASS | Limit `30.00`; the earlier two £20 rows are byte-identical; £0.01 → `limit_exceeded`, remaining `0.00`. |
| AC-31 | T-I-8 `Removing_a_limit_makes_the_category_unlimited_at_once`; script | pass | PASS | After removal, £1,000 → `approved`. |
| AC-32 | T-I-9 `Repeated_identical_request_is_a_second_transaction`; script | pass | PASS | `approved`, then `limit_exceeded`; 2 rows in history. |
| AC-33 | T-U-19 `AuthorisationServiceTests` (FakeClock); script | pass | PASS | 2026-11-04 → 11-05 approved. Also a far-future pair, 2031-03-04 → 03-05, approved; the same-day control is declined. The server clock is irrelevant. |
| AC-34 | T-I-10 `HealthTests` (incl. `Health_reports_unreachable_database_as_503`); validator live checks | local pass | PENDING-HUMAN | Local: cold start → first `200 {commit: 0cccdd5…, database: ok}` in **1.2 s**. With the DB taken offline, health gives `503 {database: unreachable}`, and it recovers without a restart within ~2 s of the DB coming back. Starting with the DB offline (BUG-008 path): the host logs `Database not ready during startup; retrying.`, does not crash, and was healthy 17 s after the DB came back online (78 s after host start, 61 s of which the DB was held offline). Still needed: the 60 s answer on `demo`, including after idle auto-pause. Run sheet R-6. |
| AC-35 | T-I-14 `P95_decision_latency_is_within_300_ms`; validator HTTP measurement | pass | PASS | T-I-14 seeds 1,000 rows and 9 limits in 3 categories, then times 200 requests after 10 warm-ups. My own run against the published host (1,000 stored transactions on Ops van via the API, 9 limits in fuel/travel/other, 200 timed requests over HTTP): **p50 1.9 ms · p95 14.8 ms · p99 17.4 ms · max 19.1 ms** (budget 300 ms). |
| AC-36 | T-E-7 `smoke/j1.spec.ts` (≤ 2 s on `demo`) | local pass | PENDING-HUMAN | The smoke spec passes against the local host (whole journey 519 ms). The local simulate API p95 is 15.9 ms. Still needed: the timing on the warmed-up `demo`. Run sheet R-4/R-7. |
| AC-37 | T-E-5 `a11y.spec.ts` (axe on both pages at 1024 and 1440 px, after errors and notices; keyboard-only J-1, J-2, J-3 with `toBeFocused` after each action); X-1, X-3 | pass | PASS | 0 critical or serious violations on Chrome and Firefox (CI and local) and on **real Edge 156** (validator). Extra axe runs on the unknown-card and non-guid "not found" states, an unknown route and a frozen card are clean as well. |
| AC-38 | T-W-1 `money.test.ts`, T-W-2 `time.test.ts`; T-E-6 `formatting.spec.ts` | pass | PASS | The limit shows `£1,234.56`; the transaction `2026-10-24T23:30:00Z` shows `25 Oct 2026 00:30` in a UTC browser zone; on 3 browsers. |
| AC-39 | T-I-11 `EventTests`; validator host-log check | pass | PASS | The mini journey ran 5 authorisations (frozen, approved, unknown, invalid, simulate) and 5 real control changes. The console OTel exporter shows exactly **5 `AuthorisationDecided`** events (`CardId`, `LastFour`, `Category`, `Amount`, `Outcome`, `Reason`, `DurationMs`; `-` for unknown/invalid) and **5 `ControlChanged`** events (`ActorId`, `ActorName`, `CardId`, `LastFour`, `Action`), with `service.name=cardlab` and `service.version=0cccdd5…`. None contains a card number. |
| AC-40 | T-U-20; script | pass | PASS | Unfreezing an active card → `active`, audit `[]`. |

## Non-functional requirements
| NFR | Measured / evidence | Target | Verdict |
|-----|---------------------|--------|---------|
| NFR-1 privacy | AC-24 and AC-39 scans: 0 card numbers in logs, responses, DB rows, UI text. The merchant rule rejects PAN-shaped runs (AC-20). ProblemDetails never echo input. | no full PAN anywhere | PASS |
| NFR-2 quality | 5 FsCheck properties in CI (AC-25); the domain path is isolated in `src/CardLab.Domain`; strictest review tier recorded in `reviews/PR-04`, `PR-08` | property tests + strict review | PASS |
| NFR-3 cost | Bicep declares only free-tier resources (AC-28 static); live tiers and cost not observable | ≤ £1/month | PENDING-HUMAN |
| NFR-4 security | OIDC only, no stored credentials, no secrets found (AC-29); GitHub secret scanning disabled (BUG-012) | no secrets; OIDC | PASS (caveat) |
| NFR-5 performance | p95 14.8 ms locally with 1,000 tx (T-I-14 green as well); local cold start 1.2 s; demo round trip and warm-up not measurable | 300 ms p95; 2 s demo; 60 s warm-up | local PASS; demo PENDING-HUMAN |
| NFR-6 a11y / i18n | axe 0 serious or critical; keyboard-only J-1..J-3; £ and London formats; Chrome + Firefox (CI) and Edge 156 (validator) all green; no horizontal scroll at 1024/1440 px; no US spellings in UI strings (grep) | as stated | PASS |
| NFR-7 observability | One OTel-shaped event per authorisation and per real change, with all fields (AC-39); resource `service.version` = commit (F-6 fix confirmed on the published host) | as stated | PASS |
| Security headers (design §10) | `/`: CSP self-only, `nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, Permissions-Policy, COOP/CORP/COEP. **HSTS** `max-age=2592000` is emitted when the request arrives with `X-Forwarded-Proto: https` and a non-localhost host (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, as `deploy.yml` sets), and not on plain HTTP. API: `Cache-Control: no-store`. ZAP 0 FAIL / 0 WARN. | §10 | PASS (live HSTS on demo: run sheet R-8) |
| Error handling | Unknown or non-guid card id → 404 ProblemDetails; bad JSON or empty body → 400 without echo; DB down → 503 "Service unavailable"; the UI shows an error notice and keeps state when an API call gives 503 (X-4); host log has 0 `fail`/`crit` entries over all journeys | §9 | PASS |

## Journeys
| Journey | Result | Notes |
|---------|--------|-------|
| J-1 Freeze and unfreeze | PASS | T-E-1, T-E-5 keyboard, script. Edges: a repeated freeze is a no-op (AC-2); unfreezing an active card is a no-op (AC-40); an unknown or non-guid card gives 404 via API and a "not found" notice in the UI (X-1). 8 simultaneous freeze requests give exactly 1 audit event; a double-click on Freeze gives 1 event (X-2). Reload keeps `Frozen`; back/forward work (X-3). |
| J-2 Set a category limit | PASS | T-E-2, script. Edges: invalid amounts give an inline error with no side effects (AC-9; UI also rejects `1,000.00`, `£5`, `abc`, `-0.01`); a second set for the same card/category/period replaces the first (AC-7; 6 concurrent PUTs give one row, all 200); lowering below spend is allowed and declines further spend (AC-30). Limits are listed in category-then-period order (F-3 fix confirmed: Travel/Month before Fuel/Day). |
| J-3 Authorise a purchase | PASS | Processor API and simulate form, on 3 browsers. Edges: frozen, limit exceeded, unknown card, malformed (15 variants), concurrency (AC-21), identical repeat (AC-32). A double-click on Submit stores 1 transaction (X-2). A 2.5 s slow response shows `Simulating…`, then the result (X-4). |

## Bugs found
| BUG | Severity | AC/R | Title |
|-----|----------|------|-------|
| BUG-011 | S4 | R-10 | `deploy.yml` header still documents rollback as "re-run workflow_dispatch on the previous green commit" |
| BUG-012 | S3 | AC-29, NFR-4 | GitHub secret scanning and push protection are disabled on the public repository; the setup guide says "keep … enabled" |

Rows for `bugs.md` (the caller appends; not edited by the validator):

```
| BUG-011 | validator | S4 | R-10 | deploy.yml header documents a rollback that cannot be done (workflow_dispatch on a previous commit) | code | | new | | | 2026-11-05 |
| BUG-012 | validator | S3 | AC-29, NFR-4 | Secret scanning and push protection disabled on the public repo; setup guide says "keep enabled" | environment | | new | | | 2026-11-05 |
```

### BUG-011
**Repro:** `sed -n 2p .github/workflows/deploy.yml` →
`# Rollback = re-run workflow_dispatch on the previous green commit (schema is forward-only).`
**Expected / actual:** the workflow says what design §14 and `docs/ops/azure-setup.md` *Rollback* say ("re-run the last
green `deploy` run; past 30 days, revert on `main`") / it describes a dispatch on an older commit. That is impossible:
dispatch runs the branch head, and the job is gated to `refs/heads/main`. Feature review F-9(f) corrected the design
text only.
**Evidence:** `deploy.yml` line 2 at `0cccdd5`; `docs/ops/azure-setup.md` section *Rollback*.
**Hint:** comment-only change.

### BUG-012
**Repro:** `gh api repos/cardlab-sim/cardlab -q '.security_and_analysis'` → `secret_scanning.status: "disabled"`,
`secret_scanning_push_protection.status: "disabled"`; `gh api repos/cardlab-sim/cardlab/secret-scanning/alerts` →
`404 Secret scanning is disabled on this repository`.
**Expected / actual:** AC-29 "secret scanning reports no secrets", and design §10 says "GitHub secret scanning stays
enabled" / scanning is off, so no report can exist and pushes are not protected. `docs/ops/azure-setup.md` §10 says
"keep secret scanning (and push protection) enabled". That wording reads as if it is already on, so the human setup
would likely skip it.
**Evidence:** the commands above (2026-11-05). The substance of AC-29 is otherwise met (see the AC table).
**Hint:** a repository setting (Settings → Code security → enable Secret Protection and push protection, free for
public repos), and "keep" → "enable" in the setup guide §10.

## Observations (not bugs)
- **Deploy is not gated on CI by itself.** `deploy.yml` runs on every push to `main`, and `main` currently has **no
  branch protection** (`gh api …/branches/main/protection` → 404). Until the human does setup §9, R-10's "a *green*
  build deploys" is not enforced. This is a documented human step (run sheet R-2), and the feature review accepted it.
- **Merging PR #3 before the Azure setup gives a red `deploy` run on `main`** (missing `vars.*` → `azure/login` fails).
  Setup §6 already expects the first run to fail at the health wait. Do setup §1-§5 before merging, or accept one red
  run (run sheet).
- Setting a limit to its current value records a `limit_changed 50.00 → 50.00` audit event (known feature-review nit;
  the requirements are silent).
- The UI rejects `1,000.00` (with a thousands separator) with an inline error. This is consistent with R-11 but is
  slight friction for a UK finance user; consider accepting the separator in a later feature.
- `ticket-key` uses the default `pull_request` types (no `edited`): editing a key out of a PR title after opening
  leaves the earlier green check in place (known nit, PR-10 N-3). AC-26 as written (on open) is met.
- A naive "16 consecutive digits" scan of the console log flags OTel span/parent ids such as `0000000000000000`.
  Future log scans should look for the seeded numbers (as T-I-7 and the reviewer's suggested CI grep do) rather than a
  bare 16-digit regex.
- `npm audit` reports 7 moderate findings in `web` dev tooling only (`typescript-eslint` → `minimatch` ReDoS);
  `npm audit --omit=dev` finds 0.
- While the database is unavailable at startup, the host retries before Kestrel listens, so requests get no answer
  (connection refused locally; on App Service the front end answers instead). Design §9 says "requests during resume
  → 503". On `demo` the platform answers during that window, which is acceptable, but the warm-up measurement (R-6)
  should note what the client actually sees.
- `Server: Kestrel` header is present (ZAP did not flag it; informational).

## Unverifiable
- **AC-27, AC-28, AC-34 (demo part), AC-36; BUG-001 and BUG-008 live; HSTS on demo; F-1 warm-up after idle.** The
  `demo` environment does not exist and no Azure call was allowed. They become verifiable after the human's one-off
  setup and the first deploy; see the run sheet.
- **AC-29 scanner clause.** Becomes verifiable once secret scanning is enabled (BUG-012) and the alerts page shows no
  open alerts.

## Human run sheet (after the one-off Azure setup)
Prerequisites: follow `docs/ops/azure-setup.md` §1-§5 (resource group, app registration + both federated credentials,
Contributor on the RG, repository variables, environment `demo` limited to `main`), then:

- **R-1 Secret scanning (AC-29, BUG-012):** Settings → Code security: enable Secret Protection / secret scanning and
  push protection. Check: `gh api repos/cardlab-sim/cardlab -q '.security_and_analysis'` shows `enabled` for both,
  and `gh api repos/cardlab-sim/cardlab/secret-scanning/alerts -q 'length'` prints `0` (wait a few minutes for the
  first history scan).
- **R-2 Branch protection (R-10, DEC-21):** setup §9, rule on `main` requiring `backend`, `web`, `e2e`, `ticket-key`,
  `analyze (csharp)`, `analyze (javascript-typescript)`, up to date before merging. Check:
  `gh api repos/cardlab-sim/cardlab/branches/main/protection -q '.required_status_checks.contexts'`.
- **R-3 First deploy:** merge PR #3 (the human's decision) → the `deploy` run starts on `main`. As setup §6 expects, it
  fails at the health wait on the first run. Do §7 (SQL user for the managed identity) and §8 (lookup key), then
  *Re-run all jobs*. If the log stream shows `Permission denied` for `/home/site/wwwroot/CardLab.Api` (exec bit lost
  by the zip deploy), set the startup command to
  `chmod +x /home/site/wwwroot/CardLab.Api && /home/site/wwwroot/CardLab.Api` (`az webapp config set -g <rg> -n <app>
  --startup-file "…"`) and re-run.
- **R-4 AC-27 / BUG-001 (deploy identifies the build):** in the green run, step "Wait for the new build to report
  healthy" prints `Healthy on <sha>` with `<sha>` = the merge commit on `main`, and step "Smoke test against demo"
  shows `1 passed` (T-E-7, which also asserts each simulate ≤ 2 s). Independently:
  `curl -s "$DEMO_URL/api/health"` → `{"commit":"<same sha as git rev-parse origin/main>","database":"ok"}`.
  Also confirm `CardLab__Commit` is **not** set:
  `az webapp config appsettings list -g <rg> -n <app> --query "[?name=='CardLab__Commit'].name" -o tsv` prints nothing.
- **R-5 AC-28 / NFR-3 (free tiers):**
  `az resource list -g <rg> --query "[].{name:name,type:type,sku:sku.name,tier:sku.tier}" -o table` lists only the plan,
  the site, the SQL server, the database (and the firewall rule). Then
  `az appservice plan show -g <rg> -n <app>-plan --query "sku.{name:name,tier:tier}"` → `F1 / Free`, and
  `az sql db show -g <rg> -s <sql-server> -n cardlab --query "{sku:currentSku.name,free:useFreeLimit,exhaust:freeLimitExhaustionBehavior,pause:autoPauseDelay}"`
  → `GP_S_Gen5_1`, `true`, `AutoPause`, `60`. Cost: portal *Cost Management → Cost analysis* for the RG after a few
  days shows ≤ £1/month forecast (expected £0.00).
- **R-6 AC-34 / NFR-5 warm-up / BUG-008 (resume after idle auto-pause):** leave `demo` untouched for **≥ 70 min**,
  then `curl -s -o /dev/null -w '%{http_code} %{time_total}\n' --max-time 60 "$DEMO_URL/api/health"` → `200` and
  `time_total` < 60. Repeat once more on another day. If it fails, check the log stream
  (`az webapp log tail -g <rg> -n <app>`) for `Database not ready during startup; retrying.` and for restarts. A
  restart loop re-opens BUG-008.
- **R-7 AC-36 (simulate ≤ 2 s warm):** after R-6 has warmed the site, either re-run the deploy's smoke step, or run
  locally `DEMO_URL=<url> npm run e2e --prefix tests/e2e -- --project smoke` (expect `1 passed`). For a raw number:
  `id=$(curl -s "$DEMO_URL/api/cards" | jq -r '.[] | select(.name=="Ops van") | .id')` then
  `curl -s -o /dev/null -w '%{time_total}\n' -X POST "$DEMO_URL/api/cards/$id/simulate-purchase" -H 'Content-Type: application/json' -d '{"amount":"1.00","category":"fuel","merchant":"Timing check"}'`
  → < 2 s. This adds one fictional transaction to Ops van; the smoke leaves the card active, and
  `curl -s "$DEMO_URL/api/cards" | jq -r '.[] | select(.name=="Ops van") | .status'` must print `active`.
- **R-8 HSTS and test endpoints on demo (design §10, DEC-16):**
  `curl -sI "$DEMO_URL/" | grep -i '^strict-transport-security'` prints a header (locally proven with forwarded
  proto), and `curl -s -o /dev/null -w '%{http_code}\n' -X POST "$DEMO_URL/api/test/reset"` → `404`.
- **R-9 Lookup key survives redeploys (setup §8):** after a second deploy,
  `az webapp config appsettings list -g <rg> -n <app> --query "[?name=='CardLab__LookupKey'].name" -o tsv` prints
  `CardLab__LookupKey`. The smoke's simulate step working proves the cards still resolve.
- **Closing out:** when R-4..R-7 pass, mark BUG-001 and BUG-008 `verified`. Re-judge AC-27, AC-28, AC-34 and AC-36 as
  PASS in this file (or add a short addendum with the run links), then G-1/G-3 are demonstrated.

## Commands run
```
# worktree (detached, removed afterwards)
git fetch origin && git worktree add --detach <scratch-worktree> origin/feat/001-card-controls   # 0cccdd5
dotnet restore CardLab.sln && npm ci --prefix web && npm ci --prefix tests/e2e
dotnet build CardLab.sln -warnaserror && npm run build --prefix web
dotnet test tests/CardLab.Domain.Tests --no-build                  # 148/0/0
npm test --prefix web -- --run                                      # 58/0
dotnet test tests/CardLab.Integration.Tests --no-build --logger trx # 66/0/0, LocalDB
dotnet format CardLab.sln --verify-no-changes; npm run lint|typecheck --prefix web; ... --prefix tests/e2e   # exit 0
npm audit --prefix web [--omit=dev]

# published host, as CI does it
dotnet publish src/CardLab.Api -c Release -o <scratch>/publish -p:SourceRevisionId=$(git rev-parse HEAD)
env ASPNETCORE_URLS=http://localhost:5080 ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__CardLab='Server=(localdb)\MSSQLLocalDB;Database=CardLab_Validate001;...' \
    CardLab__TestEndpoints=true CardLab__LookupKey=<committed dev key> dotnet CardLab.Api.dll > host.log
curl -s http://localhost:5080/api/health      # {"commit":"0cccdd5c…","database":"ok"}

# E2E
npx --prefix tests/e2e playwright install chromium firefox
BASE_URL=http://localhost:5080 npm run e2e --prefix tests/e2e -- --project local --project local-firefox   # 14 passed
DEMO_URL=http://localhost:5080 npm run e2e --prefix tests/e2e -- --project smoke                           # 1 passed
npx playwright test --config validator.config.ts   # scratch only: chrome+firefox+msedge, shipped suite + X-1..X-5: 36 passed

# scripted acceptance and scans
python -I acceptance.py http://localhost:5080 <out>       # 49 pass / 0 fail; p95 14.8 ms
curl … mini journey (freeze, frozen auth, unfreeze, set, change, approved, unknown, PAN merchant, simulate, remove)
powershell dump.ps1     # SELECT * from every table (QUOTENAME) → dbdump.txt
grep -cF -e 9999000100000001 -e …0002 -e …0003 -e …0009 -e 9999000199999999 host.log responses.txt dbdump.txt   # 0
grep -oE "EventName: +[A-Za-z]+" host.log | sort | uniq -c ; per-journey event listing   # 5 + 5 exact
powershell dbstate.ps1 OFFLINE|ONLINE   # health 503 unreachable → 200; host start with DB offline → retries → healthy
curl -sD - -H "Host: cardlab-demo.azurewebsites.net" -H "X-Forwarded-Proto: https" http://localhost:5080/   # HSTS present

# CI / repo evidence (read-only)
gh pr checks 3 ; gh pr checks 16 ; gh run view 42028992142 --log
gh run list --workflow ci --event pull_request --limit 100 → jobs: ticket-key 74 success / 5 cancelled
gh pr list --state all → 16/16 titles and bodies contain CL-1
extract `run:` of "Require the ticket key…" from ci.yml; LC_ALL=C.UTF-8 PATTERN='CL-\d+' TITLE=… BODY=… bash step.sh
gh variable list ; gh secret list ; gh api repos/cardlab-sim/cardlab (security_and_analysis) ;
gh api …/secret-scanning/alerts ; gh api …/branches/main/protection ; gh api …/environments
git log --all -p | grep -E '<secret patterns>'   # 130 commits, no secret
grep -n "secrets\." .github/workflows/*.yml     # none
# cleanup: host stopped, CardLab_Validate001 dropped, worktree removed
```
Validator-added files: none in the repository. The exploratory spec and the Edge config existed only in the scratch
worktree, uncommitted, and were removed with it.
