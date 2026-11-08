---
feature: 002-full-balance-payment
branch: feat/002-full-balance-payment
commit: 5cdf614
validated_at: 2026-11-08T10:00Z
verdict: DELIVERED WITH CAVEATS
---

# Validation: CL-3 slice 1, pay the full card balance from the linked current account

## Verdict
**DELIVERED WITH CAVEATS.** Every AC that can run without `demo` is met with fresh evidence, and the
whole pyramid is green on head `5cdf614`. The money invariants held under every probe: exactly once,
one in flight, both sides agree, and nothing moves on a refusal. The BUG-010 erratum (a steered
no-answer is recorded `Simulated = true`) holds in the running system, and a mutation proves its test.
There are three caveats. AC-26, and G-1's "against `demo`" metric, are PENDING-HUMAN because no `demo`
environment runs while Azure is parked; a local proxy passed. New BUG-012 (S3) is open: after a later
purchase, a repeated confirm or a receipt reload shows the current balance, not the original receipt
(AC-9 ⚠️). BUG-011 stays deferred (#47).

## Test runs
All runs are fresh on head `5cdf614`. Install: `dotnet restore` + `npm ci` (web, e2e). Build:
`dotnet build CardLab.sln -warnaserror --no-incremental` gave 0 warnings and 0 errors, and
`npm run build --prefix web` gave 83.10 kB gzipped JS. Lint, format and typecheck (web, e2e) all exited 0.

| Layer | Command | Passed | Failed | Skipped/todo | Duration |
|-------|---------|--------|--------|--------------|----------|
| Unit (domain + application + properties) | `dotnet test tests/CardLab.Domain.Tests` | 287 | 0 | 0 | 334 ms (3.2 s wall) |
| Unit (web) | `npm test --prefix web -- --run` | 123 (13 files) | 0 | 0 | 4.3 s |
| Integration (LocalDB) | `dotnet test tests/CardLab.Integration.Tests` | 130 | 0 | 0 | 1 m 15 s |
| E2E (chromium + firefox, isolated port 5431) | `BASE_URL=http://localhost:5431 CARDLAB_HOST_COMMAND="dotnet run --project ../../src/CardLab.Api --urls http://localhost:5431 -- --webroot ../../web/dist" npx playwright test --project local --project local-firefox` | 32 | 0 | 0 | 2.0 min |
| E2E smoke, **local proxy** (host with `TestEndpoints=false`, port 5433) | `DEMO_URL=http://localhost:5433 npx playwright test --project=smoke --repeat-each 3` | 6 | 0 | 0 | 6.2 s |
| E2E smoke on `demo` | `npm run e2e --prefix tests/e2e -- --project smoke` against `demo` | — | — | — | not runnable: no `demo` (Azure parked) |

GitHub CI on `5cdf614`: `ci` success (runs 42468270135, 42468267585) and `codeql` success. `risk-tier`
fails with "tier 3 needs 1 human approval(s); has 0" on PR #33. That is the human's approval gate, not
a defect.

Validator's own host: `dotnet run --no-build --project src/CardLab.Api --urls http://localhost:5433`
with `ConnectionStrings__CardLab=…Database=CardLabVal002` (a scratch LocalDB, dropped afterwards) and
the real 10 s bank budget. Its logs were captured for AC-23 and AC-27. The scripted checks (`T-C-n`) ran
against it with stdlib Python (`urllib`, threads), `sqlcmd` and a Playwright script. These are scratch
files, nothing was added to the repo.

## Acceptance criteria
| AC | Proof (test id / manual script) | Result | Verdict | Notes |
|----|---------------------------------|--------|---------|-------|
| AC-1 | T-U-1, T-I-1, T-I-19; T-C-2: £100 Fuel/day limit, £120.00 + £30.50 Travel approved, £500.00 Fuel declined `limit_exceeded` → cardholder view `150.50`, admin `GET /api/cards/{id}` `balanceOwed 150.50` | pass | ✅ | |
| AC-2 | T-I-2, T-E-6; T-C-4: admin `/activity` row `payment 150.50 completed`, purchases separate; balance `0.00` | pass | ✅ | |
| AC-3 | T-I-8 (failed + pending don't count); T-C-3: £100.00 purchase, failed £100 (steered account unavailable), pending £100 (steered timeout, real 10 s) → view and admin both `100.00` | pass | ✅ | T-U-2 is tautological (it passes `Money.Zero` for completed). T-C-3 and T-I-8 are the real proof. |
| AC-4 | T-I-1 (every seeded card), T-E-1; T-C-5: all 3 cards show name, last four, cardholder, "Brightline Supplies Ltd current account", 7731, available balance | pass | ✅ | |
| AC-5 | T-U-3, T-U-4, T-I-3, T-E-2 (W-7, no Pay button); browser walk: `/pay` direct URL → W-7 | pass | ✅ | |
| AC-6 | T-U-5, T-E-1 (exact W-1 lines + button in one `main`, no fake-bank region) | pass | ✅ | |
| AC-7 | T-U-10, T-I-2, T-E-1 (W-2, fee £0.00, London-format time, both accounts, £24,849.50, same reference in account activity) | pass | ✅ | |
| AC-8 | T-U-11, T-I-3 (409 W-6 £170.50, no Payment, no BankEntry), T-I-10 (refused record: shown wording, `balance_changed`, balance at refusal), T-E-2 (Review again → £170.50) | pass | ✅ | |
| AC-9 | T-U-12, T-I-4 (repeat body == original apart from `repeated`, 1 Payment, 1 BankEntry, 2 `confirm_repeated`); T-C-4: 10 parallel + 1 later repeat, identical bodies and time (BUG-008), 1 debit, 10 repeats recorded | pass | ⚠️ | **BUG-012:** when a purchase lands after completion, a repeated confirm (and a receipt reload) answers "Your card balance is now £20.00" / `balanceOwed 20.00`, not the original "£0.00". That is not "the original receipt". Exactly-once still holds. |
| AC-10 | T-U-13, T-I-5 (20 parallel); T-C-4: 10 parallel → all 200, one body, one Payment | pass | ✅ | |
| AC-11 | T-U-6, T-U-14, T-I-6 (409 `payment_pending` W-9 with amount and reference), T-E-3 (W-9 + Check status, no Pay button) | pass | ✅ | |
| AC-12 | T-U-15, T-E-2 (W-3 £150.50, fee £0.00, balance £150.50, account £25,000.00, activity empty) | pass | ✅ | |
| AC-13 | T-I-6 (1 s budget), T-E-3 (real budget, ≤ 12 s, W-5 on receipt and view, Check status); T-C-3 measured 10,010 ms | pass | ✅ | |
| AC-14 | T-U-16, T-I-7 (completed, one BankEntry, new quote allowed), T-E-3 | pass | ✅ | |
| AC-15 | T-U-17, T-I-7; T-C-3: check → failed `not_received`, bank £25,000.00, availability `available` | pass | ✅ | |
| AC-16 | T-U-18, T-I-7; T-C-3: steered `does_not_answer` → W-10 after 10,018 ms, no new `debit_requested` event, no BankEntry; next check answers | pass | ✅ | |
| AC-17 | T-U-7, T-P-4; every record read in T-C-* has one state per event, UTC (`+00:00`) times, nothing after a terminal state | pass | ✅ | |
| AC-18 | T-I-8 (both sides read after every J-1/J-3 step under all five outcomes; no fee/charge) | pass | ✅ | |
| AC-19 | T-I-9; T-C-6: Fuel/day £50, £40 Fuel, £40 payment completed → £20 Fuel declined `limit_exceeded` | pass | ✅ | |
| AC-20 | T-I-10 (all 4 states + refused: fields, wording v1, acceptance, actor, authority, events, before/after; unchanged after check/repeat/restart), T-E-6, T-W-6; BUG-010: `StatusCheckTests.Steered_no_answer_is_recorded_as_simulated` / `Unsteered_no_answer_is_not_simulated`; T-C-3 pending steered record `payment.simulated true`; T-C-7 unsteered (applock held) `simulated false` | pass | ✅ | The simulated flag is now right for pending records on both sides. |
| AC-21 | T-I-11; T-C-8: reset → 204, bank `25000.00`, 0 entries, all balances `0.00`, 0 payments | pass | ✅ | |
| AC-37 | T-I-11 (`TestEndpoints=false`); T-C-9: restarted host with `CardLab__TestEndpoints=false` on the same DB → `POST /api/test/reset` **404**; snapshot of every payment list, record, bank and view identical to before | pass | ✅ | |
| AC-22 | T-I-12; T-C-9: after a host restart the account activity still lists `PAY-2VZZ4MXJ 75.25` and the balance is still `24924.75` | pass | ✅ | |
| AC-23 | T-I-13, T-E-4 (page text, HTML, every API body), privacy.spec (001); T-C-10: 0 hits for the seeded PANs or `51907731` in host logs; SQL: no full number in `PaymentQuote`/`BankEntry`, no number column; actor `Cardholder Priya Nair` | pass | ✅ | |
| AC-24 | T-U-4, T-W-2; T-C-11: £1,000,000.00 + £0.01 → view `above_limit` with exact W-8 "£1,000,000.01"; quote → 409 `above_limit` | pass | ✅ | |
| AC-25 | T-P-1..T-P-4 (in the 287; T-P-2/T-P-3 at 1,000 cases) | pass | ✅ | Runs in CI `ci` (success on 5cdf614). |
| AC-26 | T-E-7 `smoke/pay.spec.ts` on `demo` | not run | ⛔ PENDING-HUMAN | No `demo` (Azure parked; `deploy.yml` is `workflow_dispatch` only). Local proxy on a `TestEndpoints=false` host: smoke 3/3 green, whole spec 403–600 ms; API confirm p50 5.1 / p95 7.5 / max 9.8 ms over 20 payments. BUG-011 (deferred) also threatens this on `demo`. |
| AC-27 | T-I-14; T-C-10: 66 `PaymentStateChanged` (EventId 1003) events in the host log, all with the 9 NFR-4 fields, 0 duplicate transitions, shapes none→pending→completed/failed(reason), last four only | pass | ✅ | |
| AC-28 | T-E-5 (axe on view, review, receipt, then a keyboard-only journey on chromium and firefox) + the 768 px spec (F7) | pass | ✅ | |
| AC-30 | T-U-19, T-I-3 (repeat → 409 `quote_refused`, same W-6, no Payment, new quote has a new reference) | pass | ✅ | |
| AC-31 | T-I-15, T-I-2; T-C-12: £25,000.00 + £0.01, steered complete → failed `insufficient_funds`, bank `25000.00` | pass | ✅ | |
| AC-32 | T-I-6 `Timeout_steering_applies_to_one_request`; T-C-3 the steering reverted (next check answered) | pass | ✅ | |
| AC-33 | T-E-4 (no fake-bank region or control on review/receipt; record simulated), T-W-5; BUG-010 tests + T-C-3 | pass | ✅ | |
| AC-34 | T-I-7, T-E-3 (view open → completed, effective = bank debit time), T-E-6 (admin open); browser walk D: opening admin detail settled `PAY-0QN3BDXG` (`status_check_completed`) | pass | ✅ | |
| AC-38 | T-U-22 (EffectiveAt = SettledAt = T2, no BankDebitedAt) | pass | ✅ | Only the application layer can reach this: the shipped fake always reports a debit time. |
| AC-36 | T-U-21, T-I-7 (before £170.50, after £20.00, settled £150.50) | pass | ✅ | |
| AC-35 | T-U-20, T-E-2 (W-4 £150.50, fee £0.00) | pass | ✅ | |
| AC-29 | T-C-1: `compliance-preread.md` CD-1..CD-8 all `decided`; a scripted diff of §17 W-1..W-10 against `WordingV1.cs` literals gives **10/10 verbatim**; the wording ACs quote §17 | pass | ✅ | |

**Tally: 36 ✅ · 0 ❌ · 1 ⚠️ (AC-9) · 1 ⛔ PENDING-HUMAN (AC-26).**

### Fixed bugs re-verified (ledger moves `fixed` → `verified`)
| BUG | Symptom re-tested (this session) | Regression test |
|---|---|---|
| BUG-002 | T-C-13: held the bank applock 4 s, confirm client gave up after 1 s (`TimeoutError`), and the payment still settled **completed** 3.2 s later (`bank_completed`, bank −150.50) | `Client_disconnect_never_cancels_the_debit_and_the_payment_settles` green |
| BUG-003 | T-C-14: a check sent 1 s into a hanging confirm (`timeout_debit_applied`) returned at 10,104 ms, *after* the confirm (10,086 ms). It never asked the bank early and created no second debit (1 BankEntry) | `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget` green (cap is unit-only: it can't be reached with the shipped fake) |
| BUG-004 | T-C-3: steered `timeout_not_received`, then check → failed `not_received`, `payment.simulated true`, events `status_check_not_received*` | `Not_received_is_simulated_only_when_steered` green |
| BUG-005 | SQL `SELECT Origin … FROM PaymentQuote` → `CardholderView` / `DeploySmoke`; wire `origin: deploy_smoke` only for the exact header `deploy-smoke` (`Deploy-Smoke` → `cardholder_view`); smoke record shows "origin: smoke" | `OriginMarkerTests` green; design grep clean |
| BUG-007 | `ActivityFake` gone (`grep` empty); shared fake feeds `simulatePurchase` into `listActivity`/balance (T-W-10, T-W-8 asserts a consistent £32.50) | web 123/123 |
| BUG-008 | T-C-4: 10 parallel confirms + 1 later repeat → one `time` value `…:33.926+00:00`, bodies identical apart from `repeated` | T-I-4/T-I-5 exact equality green |
| BUG-010 | T-C-3: steered debit no-answer → `bank_unanswered` `simulated true`, `payment.simulated true`; steered `does_not_answer` → `status_check_unanswered` `simulated true`; T-C-7 unsteered (applock held) → `simulated false`. **Mutation** (throwaway worktree, `PaymentService.cs:426` `Unknown(e is SimulatedNoAnswerException)` → `Unknown(e is null)`): 2 unit + 1 integration tests red; green on HEAD | `Steered_no_answer_is_recorded_as_simulated` (unit + integration), `Steered_unanswered_check_is_recorded_as_simulated`, `Unsteered_no_answer_is_not_simulated`, `FakeBank_hangs_until_the_callers_token_fires` |

## Non-functional requirements
| NFR | Measured / evidence | Target | Verdict |
|-----|---------------------|--------|---------|
| NFR-1 | Rules live in `src/CardLab.Domain/Payments`; T-P-1..T-P-4 green; tier-3 reviews of PR-01/04/09 on record | pure domain + properties | ✅ |
| NFR-2 | Local, warm, bank complete: confirm p50 5.1 / p95 7.5 / max 9.8 ms; view GET p95 3.2 ms (3,990 B); quote p95 17.2 ms; smoke spec 403–600 ms end to end; T-I-17 view p95 ≤ 300 ms green. Bank budget: pending after 10,010 ms, W-10 after 10,018 ms; browser pending receipt after 10,044 ms. | ≤ 3 s confirm, ≤ 2 s review **on `demo`**; 10 s budget | ✅ budget / ⛔ demo timings PENDING-HUMAN |
| NFR-3 | Logs, DB rows and API bodies scanned (T-C-10, T-I-13, T-E-4): last four only; the fake needs no secrets | no full numbers | ✅ |
| NFR-4 | 66 events, fields complete, one per change (T-C-10); `BankCallUnanswered` only on real or steered no-answers (F9) | one structured event per change | ✅ |
| NFR-5 | axe 0 critical/serious on view/review/receipt (both browsers); keyboard-only journey; 768 px no horizontal scroll; UK money and London times; states shown as text badges | 0 serious/critical | ✅ |
| NFR-6 | `git diff origin/main...HEAD -- infra .github Directory.Packages.props */package.json`: no changes | no new resources | ✅ |
| NFR-7 | AC-29 (all CDs decided; wording 10/10 verbatim; record = fields + text + version) | OB-1/3/7/8/9 | ✅ |
| Security headers (001) | Cardholder API: CSP `default-src 'self' …; frame-ancestors 'none'`, `X-Content-Type-Options nosniff`, `X-Frame-Options DENY`, `Referrer-Policy no-referrer`, `Cache-Control no-store` | unchanged from 001 | ✅ |
| Bad input | Steering with an unknown value → 400 ProblemDetails; non-JSON → 400; malformed, injection-like or 5,000-char references → 404; unknown or non-GUID card → 404; garbage quote body ignored (201) | safe rejection | ✅ |

## Journeys
| Journey | Result | Notes |
|---------|--------|-------|
| J-1 pay the full balance | ✅ | T-E-1 on both browsers. Browser walk: with the confirm request delayed 2.5 s, three clicks sent **one** confirm; reloading the receipt shows identical text; Back from the receipt mints a new review, which shows W-7 because there is nothing to pay. Edge paths: £0.00 (W-7), balance changed (W-6), refusals (W-3/W-4), timeout (W-5), double submit: all exercised. |
| J-2 see the payment on both sides | ✅ | T-E-1, T-E-6, T-I-8: payment rows in card and account activity, admin history marks it "Payment", a failed payment shows on the card side only, a pending one is not counted. |
| J-3 resolve a pending payment | ✅ | T-E-3, T-I-7, T-C-3: Check status, auto check on view and admin open, an unanswered check stays pending (W-10), a purchase during pending is allowed (AC-36). |

## Bugs found
| BUG | Severity | AC/R | Title |
|-----|----------|------|-------|
| BUG-012 | S3 | AC-9, R-7, R-12 | A repeated confirm or receipt reload after a later purchase shows the current balance, not the original receipt |

## Observations (not bugs)
- A pending receipt page does not auto-check on reload; only the cardholder view and admin detail do. This
  matches R-11 and DEC-13. A cardholder who refreshes the receipt sees "pending" until they press Check
  status or go back to the card.
- Every render of the review page mints and stores a new quote, and quotes are kept 6 years. Back, refresh
  or abandoning the review therefore leaves `open` quotes on the record (by design, R-7 and DEC-8). Slice 4
  history views should filter them.
- Two different open quotes confirmed at the same instant: one completes, the other gets W-9 ("still
  pending") for a payment that completes milliseconds later. Its reference stays `open`, and
  `confirm_refused`/`payment_pending` is recorded. This is safe, and the wording is momentarily stale.
- A frozen card can still be paid (quote and confirm succeed). The requirements don't cover this, and it
  is sensible behaviour.
- An unsteered bank contention path: the fake's 5 s applock timeout surfaces as an adapter failure
  (`bank_unanswered`, `Detail InvalidOperationException`, `simulated false`) before the 10 s budget. This
  is consistent with §9.
- T-U-2 (AC-3) asserts nothing beyond `Owed(100, 0) = 100`. AC-3's real proof is T-I-8 plus T-C-3. If T-U-2
  were made meaningful it should go through the store.
- CI hygiene outside this feature: `.github/workflows/findings-triage.yml` (CL-2) fails on every push with
  no jobs (19 runs on this branch). `npm ci --prefix tests/e2e` reports 7 moderate advisories. No package
  changes in this feature.

## Unverifiable
- **AC-26 / NFR-2 "on `demo`" / G-1 metric "Playwright journey against `demo`": PENDING-HUMAN.** Azure is
  parked on `main` and this branch (`deploy.yml` is `workflow_dispatch` only, DORA-lite uses
  `dora-deploy.yml`), so no `demo` environment exists to measure. To make it verifiable: bring `demo` back,
  run `deploy.yml` (its smoke runs T-E-7), and resolve BUG-011 first (option a or b in #47), because a
  drained demo bank turns T-E-7 red. The local proxy above passed.

## Commands run
```
git fetch; git status  (feat/002-full-balance-payment == origin, head 5cdf614, clean)
dotnet restore CardLab.sln && npm ci --prefix web && npm ci --prefix tests/e2e
dotnet build CardLab.sln -warnaserror --no-incremental && npm run build --prefix web
dotnet test tests/CardLab.Domain.Tests --no-build
npm test --prefix web -- --run
dotnet test tests/CardLab.Integration.Tests --no-build
dotnet format CardLab.sln --verify-no-changes; npm run lint --prefix web; npm run lint --prefix tests/e2e; npm run typecheck --prefix web; npm run typecheck --prefix tests/e2e
cd tests/e2e; BASE_URL=http://localhost:5431 CARDLAB_HOST_COMMAND="dotnet run --project ../../src/CardLab.Api --urls http://localhost:5431 -- --webroot ../../web/dist" npx playwright test --project local --project local-firefox
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__CardLab="…Database=CardLabVal002…" dotnet run --no-build --project src/CardLab.Api --urls http://localhost:5433 -- --webroot D:/cardlab/web/dist
  (restarted once with CardLab__TestEndpoints=false for AC-37/AC-22 and the smoke proxy, then with it on again)
python -I acc.py ac1|ac3|ac24|ac19|ac31|ac_completion_and_repeat   (scripted T-C checks)
sqlcmd … sp_getapplock 'bank:5a3e1d2c-7b4f-4c1a-9e2d-000000000731' (BUG-002 disconnect, unsteered no-answer)
DEMO_URL=http://localhost:5433 npx playwright test --project=smoke --repeat-each 3
python explore.py; node browse.cjs   (exploratory: bad input, concurrency, refresh/back/slow network)
python wording.py 01-requirements.md WordingV1.cs   (AC-29: 10/10 verbatim)
git worktree add --detach <scratch>/wt-mut HEAD; mutate PaymentService.cs:426; dotnet build; dotnet test --filter Steered / StatusCheckTests; git worktree remove
dotnet test … --filter <fixed-bug regression tests>   (domain 5/5, integration 9/9)
gh run list --branch feat/002-full-balance-payment; gh run view 42468270285 --log-failed
```
