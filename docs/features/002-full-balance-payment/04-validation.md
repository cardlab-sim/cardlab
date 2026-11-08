---
feature: 002-full-balance-payment
branch: feat/002-full-balance-payment
commit: 25491ee
validated_at: 2026-11-08T10:35Z
first_validated: 5cdf614 (2026-11-08T10:00Z)
verdict: DELIVERED WITH CAVEATS
---

# Validation: CL-3 slice 1, pay the full card balance from the linked current account

## Verdict
**DELIVERED WITH CAVEATS** (re-validated on head `25491ee`, after PR-10 #49). BUG-012 is fixed and
**verified**. A repeated confirm, a receipt reload and a check on a settled payment now return the
original receipt after a later purchase, and AC-9 moves from ⚠️ to ✅. Every receipt AC was re-judged
against the live system and holds. The whole pyramid is green again on `25491ee`. The money invariants
still hold: exactly once, one in flight, both sides agree, and nothing moves on a refusal.

One caveat remains. AC-26, and G-1's "against `demo`" metric, are PENDING-HUMAN because no `demo`
environment runs while Azure is parked. A local proxy passed. BUG-011 stays deferred (#47). No new bugs.

(First validation on `5cdf614`: DELIVERED WITH CAVEATS, with AC-9 ⚠️ for BUG-012. That run's evidence
is kept below. The re-validation section has the fresh evidence.)

## Re-validation 2026-11-08 (head `25491ee`, BUG-012 / PR-10)
**Scope.** PR-10 changed only `PaymentService.BuildReceipt` and `StoredReceiptAsync`. A settled receipt
now shows `Settlement.BalanceAfter`, and a pending one shows the current owed (design §5, BUG-012
erratum). Re-checked: the BUG-012 repro, the failed, check-settled and pending variants, every AC that
touches the receipt (AC-7, AC-9, AC-10, AC-12, AC-13, AC-14, AC-15, AC-35, AC-36), and the full pyramid
as a regression check.

**Setup.** `git pull` (`feat/002-full-balance-payment` == origin, head `25491ee`, clean). Clean
`dotnet restore` plus `npm ci` (web, e2e). `dotnet build CardLab.sln -warnaserror --no-incremental`
gave 0 warnings and 0 errors, and `npm run build --prefix web` gave 83.10 kB gzipped JS (SPA built
before E2E). `dotnet format --verify-no-changes`, the web and e2e lint, and the web and e2e typecheck
all exited 0.

| Layer | Command | Passed | Failed | Skipped/todo | Duration |
|-------|---------|--------|--------|--------------|----------|
| Unit (domain + application + properties) | `dotnet test tests/CardLab.Domain.Tests --no-build` | 291 (+4 PR-10) | 0 | 0 | 349 ms (3.3 s wall) |
| Unit (web) | `npm test --prefix web -- --run` | 123 (13 files) | 0 | 0 | 4.4 s |
| Integration (LocalDB) | `dotnet test tests/CardLab.Integration.Tests --no-build` | 131 (+1 T-I-4b) | 0 | 0 | 1 m 18 s |
| E2E (chromium + firefox, isolated port 5431, SPA built first) | `BASE_URL=http://localhost:5431 CARDLAB_HOST_COMMAND="dotnet run --project ../../src/CardLab.Api --urls http://localhost:5431 -- --webroot ../../web/dist" npx playwright test --project local --project local-firefox` | 32 | 0 | 0 | 2.2 min |

GitHub CI on `25491ee`: `ci` success (runs 42471048977, 42471046748). `codeql` **failed for an
environmental reason**: `analyze (csharp)` stopped in `codeql-action/init` with "Unable to download and
extract CodeQL CLI: Unexpected HTTP response: 504". `analyze (javascript-typescript)` succeeded. A
re-run is needed before the final PR. `risk-tier` still needs the human's approval. `findings-triage.yml`
still fails with no jobs (CL-2, as before).

**Regression tests proved red to green (mutation).** In a throwaway worktree at `25491ee`, I reverted
the rule: `BuildReceipt` prefers the passed balance over `Settlement.BalanceAfter`, and
`StoredReceiptAsync` always reads the current owed. Four domain tests went red:
`Repeated_confirm_after_a_later_purchase_returns_the_original_receipt`,
`Failed_receipt_reload_after_a_later_purchase_keeps_its_balance`,
`Receipt_of_a_payment_settled_by_check_keeps_the_settlement_balance` and the amended T-U-38
`Receipt_reload_reads_without_calling_the_bank` (4 failed / 287 passed). One integration test went red:
`IdempotencyTests.Repeat_and_reload_after_a_later_purchase_return_the_original_receipt` (T-I-4b, 1 of
3). All five are green on HEAD. `Pending_receipt_shows_the_current_balance` pins the pending rule. It
is green before and after the fix, as the triage intended. I removed the worktree, and the main
checkout was not touched.

**Live host.** `dotnet run --no-build --project src/CardLab.Api --urls http://localhost:5435` with
`ASPNETCORE_ENVIRONMENT=Development` (TestEndpoints=true) on a scratch LocalDB `CardLabReval002`
(dropped afterwards), with the real 10 s bank budget. Scratch scripts only, nothing added to the repo.

| Check | What was run | Result |
|---|---|---|
| T-C-15, BUG-012 curl repro (exact ledger steps) | reset; £150.50 purchase; quote; confirm; £20.00 purchase; repeat confirm; receipt GET | 1st confirm `completed`, `balanceOwed "0.00"`, "… Your card balance is now £0.00. Reference PAY-S0SJZQCR." The repeat body is **byte-identical apart from `repeated: true`**: same `time` `10:24:30.801+00:00`, `balanceOwed "0.00"`, "now £0.00". The GET body is identical to the first. Record: `balanceBefore 150.50`, `balanceAfter 0.00`, events `confirm_created, debit_requested, bank_completed, confirm_repeated`. Card view (live) `20.00`. **Fixed.** |
| T-C-16 S1, completed | as T-C-15, plus a check on the settled payment | repeat == original (repeated aside), reload == original, check == original; all `0.00` / "now £0.00"; one debit |
| T-C-16 S2a, failed, insufficient funds (AC-12) | steer `refuse_insufficient_funds`; confirm → W-3; new quote, confirm → completed `0.00`; £20.00 purchase; reload, check and repeat of the failed reference | the failed receipt keeps "Your card balance is **still £150.50**" and `balanceOwed 150.50` on reload, on check and on repeat (repeat differs only in `repeated`). The completed receipt reload stays `0.00`. Before the fix this read "still £0.00". |
| T-C-16 S2b, failed, account unavailable (AC-35) | same with `refuse_account_unavailable` → W-4 | same: "still £150.50" kept on reload, check and repeat; fee `0.00` |
| T-C-16 S3, pending settled by a check (AC-13, AC-14, AC-36) | steer `timeout_debit_applied`; confirm; £20.00 purchase; reload; check; £5.00 purchase; reload, second check and repeat | pending after **10.02 s**, W-5 "shows £150.50", fee `0.00`. The pending reload after the purchase shows the **current** "shows £170.50", and so does the card view's pending notice (same message). The check gives completed "now £20.00". After the £5.00 purchase the reload, second check and repeat all keep "now £20.00" / `20.00` (not £25.00), while the live view says `25.00` and `available`. One debit. |
| T-C-16 S4, pending then not received (AC-15) | steer `timeout_not_received`; confirm; £20.00 purchase; check; £5.00 purchase; reload | the check gives failed "still £170.50" (the settlement's BalanceAfter). The bank stays `25000.00` and the card can pay again. The reload after the £5.00 purchase still reads "still £170.50". |
| T-C-16 S5, 10 parallel confirms (AC-10) | 10 parallel; £20.00 purchase; one later repeat and a reload | all 200 `completed`, one body (apart from `repeated`); the later repeat and the reload equal it; one debit; 10 `confirm_repeated` events (9 parallel + 1 later) |
| T-C-17, browser (chromium, Playwright script) | confirm from `/cardholder/{ops}/pay`; £20.00 purchase; page reload; deep link | the receipt page text after the reload and the deep link is **identical** to the first render ("now £0.00"), and the card view shows £20.00. Pending receipt page after a £20.00 purchase: "shows £170.50". Check status → "now £20.00". A £5.00 purchase and reload → still "now £20.00". AC-36 record: amount `150.50`, before `170.50`, after `20.00`. |
| T-C-18, host restart | a check-settled payment ("now £20.00"), £5.00 purchase, host killed and restarted on the same DB | receipt GET `completed`, `20.00`, "now £20.00"; view `25.00`. The receipt is rebuilt from the stored settlement. |
| Exploratory | repeat confirm while pending after a purchase; unanswered check (`does_not_answer`); cross-card and unknown references | a pending repeat and the W-10 receipt show the current `170.50` (the pending rule). After W-10, a reload reads W-5 with `170.50`, and the next check completes with `20.00`. The receipt GET, confirm and check under another card's id all return 404, and an unknown reference returns 404. |

**Re-judged receipt ACs.** AC-7 ✅ · AC-9 ✅ (was ⚠️) · AC-10 ✅ · AC-12 ✅ · AC-13 ✅ · AC-14 ✅ · AC-15 ✅ ·
AC-35 ✅ · AC-36 ✅. The AC table below carries the updated rows.

## Test runs (first validation, head `5cdf614`)
For the fresh re-run on `25491ee`, see the re-validation section above. All runs here are fresh on head `5cdf614`. Install: `dotnet restore` + `npm ci` (web, e2e). Build:
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
| AC-7 | T-U-10, T-I-2, T-E-1 (W-2, fee £0.00, London-format time, both accounts, £24,849.50, same reference in account activity); re-val T-C-15, T-C-17 | pass | ✅ | Re-val `25491ee`: W-2 "now £0.00" is now also what every later reload of the receipt shows. |
| AC-8 | T-U-11, T-I-3 (409 W-6 £170.50, no Payment, no BankEntry), T-I-10 (refused record: shown wording, `balance_changed`, balance at refusal), T-E-2 (Review again → £170.50) | pass | ✅ | |
| AC-9 | T-U-12, T-I-4, **T-I-4b** `Repeat_and_reload_after_a_later_purchase_return_the_original_receipt`, `Repeated_confirm_after_a_later_purchase_returns_the_original_receipt`; T-C-4; re-val T-C-15, T-C-16 S1/S2/S3/S5, T-C-17 | pass | ✅ | Re-val `25491ee`: **BUG-012 verified.** After a later purchase the repeat is byte-identical to the original apart from `repeated` (`balanceOwed 0.00`, "now £0.00"). One Payment, one debit, each repeat recorded as `confirm_repeated` with its time. Mutation turns the new tests red. (5cdf614: ⚠️ BUG-012.) |
| AC-10 | T-U-13, T-I-5 (20 parallel); T-C-4: 10 parallel → all 200, one body, one Payment; re-val T-C-16 S5 | pass | ✅ | Re-val: same outcome is kept by a later repeat or reload after a purchase. |
| AC-11 | T-U-6, T-U-14, T-I-6 (409 `payment_pending` W-9 with amount and reference), T-E-3 (W-9 + Check status, no Pay button) | pass | ✅ | |
| AC-12 | T-U-15, T-E-2 (W-3 £150.50, fee £0.00, balance £150.50, account £25,000.00, activity empty); `Failed_receipt_reload_after_a_later_purchase_keeps_its_balance`; re-val T-C-16 S2a | pass | ✅ | Re-val: the W-3 receipt keeps "still £150.50" on reload, check and repeat after a later completed payment and purchase. |
| AC-13 | T-I-6 (1 s budget), T-E-3 (real budget, ≤ 12 s, W-5 on receipt and view, Check status); T-C-3 measured 10,010 ms; `Pending_receipt_shows_the_current_balance`; re-val T-C-16 S3 (10.02 s) | pass | ✅ | Re-val: W-5 £150.50, fee £0.00. While pending, the receipt shows the current owed by design (§5 erratum). After an AC-36 purchase it reads "shows £170.50", the same as the card view's notice. |
| AC-14 | T-U-16, T-I-7 (completed, one BankEntry, new quote allowed), T-E-3; `Receipt_of_a_payment_settled_by_check_keeps_the_settlement_balance`; re-val T-C-16 S3, T-C-17, T-C-18 | pass | ✅ | Re-val: a check-settled receipt keeps its settlement balance on reload, a second check, a repeat and after a host restart. One debit; the card can pay again. |
| AC-15 | T-U-17, T-I-7; T-C-3: check → failed `not_received`, bank £25,000.00, availability `available`; re-val T-C-16 S4 | pass | ✅ | Re-val: the failed receipt keeps its settlement balance ("still £170.50" after an intervening £20.00 purchase) on reload after a later purchase. Nothing moved; the card can pay again. |
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
| AC-36 | T-U-21, T-I-7 (before £170.50, after £20.00, settled £150.50); re-val T-C-16 S3, T-C-17 | pass | ✅ | Re-val: record amount 150.50, before 170.50, after 20.00; the check's receipt "now £20.00" is kept after a later £5.00 purchase. |
| AC-35 | T-U-20, T-E-2 (W-4 £150.50, fee £0.00); re-val T-C-16 S2b | pass | ✅ | Re-val: the W-4 receipt keeps "still £150.50" on reload, check and repeat after a later completed payment and purchase. |
| AC-29 | T-C-1: `compliance-preread.md` CD-1..CD-8 all `decided`; a scripted diff of §17 W-1..W-10 against `WordingV1.cs` literals gives **10/10 verbatim**; the wording ACs quote §17 | pass | ✅ | |

**Tally (re-validation, `25491ee`): 37 ✅ · 0 ❌ · 0 ⚠️ · 1 ⛔ PENDING-HUMAN (AC-26).**
(First validation, `5cdf614`: 36 ✅ · 0 ❌ · 1 ⚠️ (AC-9) · 1 ⛔.)

### Fixed bugs re-verified (ledger moves `fixed` → `verified`)
| BUG | Symptom re-tested (this session) | Regression test |
|---|---|---|
| BUG-002 | T-C-13: held the bank applock 4 s, confirm client gave up after 1 s (`TimeoutError`), and the payment still settled **completed** 3.2 s later (`bank_completed`, bank −150.50) | `Client_disconnect_never_cancels_the_debit_and_the_payment_settles` green |
| BUG-003 | T-C-14: a check sent 1 s into a hanging confirm (`timeout_debit_applied`) returned at 10,104 ms, *after* the confirm (10,086 ms). It never asked the bank early and created no second debit (1 BankEntry) | `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget` green (cap is unit-only: it can't be reached with the shipped fake) |
| BUG-004 | T-C-3: steered `timeout_not_received`, then check → failed `not_received`, `payment.simulated true`, events `status_check_not_received*` | `Not_received_is_simulated_only_when_steered` green |
| BUG-005 | SQL `SELECT Origin … FROM PaymentQuote` → `CardholderView` / `DeploySmoke`; wire `origin: deploy_smoke` only for the exact header `deploy-smoke` (`Deploy-Smoke` → `cardholder_view`); smoke record shows "origin: smoke" | `OriginMarkerTests` green; design grep clean |
| BUG-007 | `ActivityFake` gone (`grep` empty); shared fake feeds `simulatePurchase` into `listActivity`/balance (T-W-10, T-W-8 asserts a consistent £32.50) | web 123/123 |
| BUG-008 | T-C-4: 10 parallel confirms + 1 later repeat → one `time` value `…:33.926+00:00`, bodies identical apart from `repeated` | T-I-4/T-I-5 exact equality green |
| BUG-012 (re-val `25491ee`) | T-C-15: the exact ledger repro. The repeat and the GET after a later £20.00 purchase both give `balanceOwed 0.00` and "now £0.00", identical to the first receipt. Variants T-C-16 to T-C-18 cover failed, check-settled, pending and restart. | `Repeated_confirm_after_a_later_purchase_returns_the_original_receipt`, `Failed_receipt_reload_after_a_later_purchase_keeps_its_balance`, `Receipt_of_a_payment_settled_by_check_keeps_the_settlement_balance`, amended T-U-38, T-I-4b, all green. The mutation turns 4 unit tests and 1 integration test red. `Pending_receipt_shows_the_current_balance` is green (it pins the rule). |
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
| BUG-012 | S3 | AC-9, R-7, R-12 | A repeated confirm or receipt reload after a later purchase shows the current balance, not the original receipt. **Verified fixed on `25491ee` (PR-10 #49).** |

Re-validation on `25491ee` found no new bugs.

## Observations (not bugs)
- (re-val) A **pending** receipt follows the live balance, as the BUG-012 decision intends (§5 erratum). After an
  AC-36 purchase, a reload reads "Your card balance shows £170.50 until we know the outcome", where the first
  pending receipt said £150.50. It matches the card view's notice, and W-5's "shows" wording reads as live.
  A repeat confirm and the W-10 receipt while pending follow the same rule.
- (re-val) GitHub `codeql` on `25491ee` is red only because the CodeQL bundle download returned HTTP 504
  (`analyze (csharp)` init step). Re-run it before the final PR.
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

# re-validation, head 25491ee
git fetch; git pull  (== origin, head 25491ee, clean)
dotnet restore CardLab.sln && npm ci --prefix web && npm ci --prefix tests/e2e
dotnet build CardLab.sln -warnaserror --no-incremental && npm run build --prefix web
dotnet test tests/CardLab.Domain.Tests --no-build; npm test --prefix web -- --run; dotnet test tests/CardLab.Integration.Tests --no-build
git worktree add --detach <scratch>/wt-mut HEAD; revert the BUG-012 rule in PaymentService.cs (BuildReceipt + StoredReceiptAsync);
  dotnet test tests/CardLab.Domain.Tests (4 red); dotnet test tests/CardLab.Integration.Tests --filter IdempotencyTests (1 red); git worktree remove
dotnet test tests/CardLab.Integration.Tests --no-build --filter IdempotencyTests  (HEAD: 3/3)
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__CardLab="…Database=CardLabReval002…" dotnet run --no-build --project src/CardLab.Api --urls http://localhost:5435 -- --webroot D:/cardlab/web/dist
  (killed and restarted once on the same DB for T-C-18; DB dropped afterwards)
bash bug012-curl.sh http://localhost:5435        (T-C-15)
python -I reval.py http://localhost:5435          (T-C-16 S1..S5: 39/39 checks)
cd tests/e2e; node browse.cjs http://localhost:5435  (T-C-17: 9/9)
curl probes: pending repeat, does_not_answer check, cross-card and unknown references, restart reload (T-C-18)
cd tests/e2e; BASE_URL=http://localhost:5431 CARDLAB_HOST_COMMAND="dotnet run --project ../../src/CardLab.Api --urls http://localhost:5431 -- --webroot ../../web/dist" npx playwright test --project local --project local-firefox
dotnet format CardLab.sln --verify-no-changes; npm run lint|typecheck --prefix web|tests/e2e
gh run list --branch feat/002-full-balance-payment; gh run view 42471048947 --log-failed
```
