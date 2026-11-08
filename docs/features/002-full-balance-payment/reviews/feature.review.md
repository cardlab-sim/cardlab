# Feature review: 002-full-balance-payment (CL-3 slice 1)

- Mode: `feature` · branch `origin/feat/002-full-balance-payment` @ `aa4c956` (base `main`) · risk class R2 · lane M
- Inputs: `01-requirements.md` (R-1..R-17, NFR-1..NFR-7, AC-1..AC-38, §17 wording v1), `02-tech-design.md` (DEC-1..DEC-15 and
  errata BUG-002..BUG-005, BUG-009), `02-design-digest.md`, `03-pr-plan.md`, `prs/*.md`, `reviews/PR-*.review.md`, `bugs.md`,
  `compliance-preread.md`, ADR-006, ADR-007, and the full code diff `origin/main...origin/feat/002-full-balance-payment`
  (177 files).

## Verdict: **CHANGES**

| Severity | Count |
|---|---|
| blocker | 1 (F1, `upstream:design` + code) |
| major | 1 (F2, `upstream:requirements`) |
| minor | 7 |
| nit | 1 line at the end |

The money core is sound. I traced exactly-once, one-in-flight, the three-step confirm, the unlinked bank budget, the in-flight
registry, the balance and settlement arithmetic, wording v1, append-only records and privacy end to end. I found no path
that pays twice or applies to one side only. The one blocker is in the compliance record: an outcome produced by steering
the fake bank to "not answer" is never marked simulated. DEC-7 and R-15 both require that mark. Everything else is doc drift,
test gaps and small cleanups.

## Evidence (fresh, this session, head `aa4c956`)
- Build: `dotnet build CardLab.sln -warnaserror`: 0 warnings, 0 errors.
- Unit: `dotnet test tests/CardLab.Domain.Tests`: **283 passed, 0 failed, 0 skipped**. `npm test --prefix web -- --run`:
  **13 files, 123 passed, 0 failed**.
- Integration (LocalDB): `dotnet test tests/CardLab.Integration.Tests`: **128 passed, 0 failed, 0 skipped** (1 m 14 s).
- Lint and typecheck: `dotnet format --verify-no-changes`, `npm run lint` (web, e2e) and `npm run typecheck` (web, e2e)
  all exit 0.
- E2E, full suite on an isolated port: built the SPA, then ran `BASE_URL=http://localhost:5417`
  `CARDLAB_HOST_COMMAND="dotnet run --project ../../src/CardLab.Api --urls http://localhost:5417 -- --webroot ../../web/dist"`
  `npx playwright test --project local --project local-firefox`. Result: **30 passed, 0 failed** (2.2 min), covering the
  001 and 002 specs on chromium and firefox. I did not run the smoke project: it targets `demo`, and the orchestrator's run
  covered it locally.
- Doc lint: requirements, design, plan and state all **PASS** (0 errors, 0 warnings).
- Leftover stubs: `git grep` finds no `NotImplementedException`, `Skip = "todo"`, `test.todo`, `test.fixme`, `TODO` or
  `FIXME` under `src`, `web/src` or `tests`.
- CI: `ci.yml` runs the domain and integration suites, web vitest, lint and typecheck, and the e2e job
  (`--project local --project local-firefox`, which picks up `src/payments/**`) plus the ZAP baseline. `deploy.yml` runs
  `--project smoke`, which includes `smoke/pay.spec.ts`. The new tests are all covered.
- Public-repo hygiene: no local paths and no non-product names in tracked files.

## Findings

| # | Severity | Class | Where | Finding | Why it matters | Fix | Check |
|---|---|---|---|---|---|---|---|
| F1 | **blocker** | `upstream:design` (port contract erratum) + code | `src/CardLab.Domain/Application/PaymentService.cs:419-421` (`catch (OperationCanceledException) when (budget…)` → `Unknown(false)`), `:501` (the unanswered event is appended with the default `simulated: false`); `src/CardLab.Infrastructure/FakeBank/SqlFakeBank.cs:72`, `:114` (a steered hang ends in a plain OCE); `src/CardLab.Api/Contracts/PaymentDtoMapper.cs` (`payment.simulated = Settlement?.Simulated ?? false`); web fake `web/src/api/fakeApiClient.ts:494`, `:509-511` | **A steered "no answer" is recorded as a real bank non-answer.** When the panel is set to *time out, debit applied*, *time out, not received* or *Next status check: does not answer*, the fake bank hangs until the budget fires. The service cannot tell that hang from a real one, so it records `bank_unanswered` / `status_check_unanswered` with `Simulated = false`. A pending payment's record then shows `simulated: false`. A `does_not_answer` check is never marked at all, because no settlement follows it. The design says the opposite: DEC-7 (02-tech-design.md:167) says "the unanswered check is recorded as `status_check_unanswered` with `Simulated = true`". The shared web `FakeApiClient` does mark these events simulated, so the SPA's fake and the real API disagree. No test pins the real behaviour. | R-15: "Outcomes produced under a steering setting are marked as simulated in the payment record". R-14 and AC-20 require "whether the outcome was simulated" for every state, including pending. CQ-8 / decision 23 exist to "keep records honest". On `demo`, a visitor's steered timeout becomes a permanent 6-year compliance record (CD-5) that reads as a genuine bank failure. The validator will fail AC-20 on a pending steered record. | **Design erratum** (§5 `ICurrentAccountBank` contract, DEC-7): "A no-answer produced by steering ends, when the caller's token fires, with `SimulatedNoAnswerException : OperationCanceledException` (`CardLab.Domain.Ports`); any other no-answer ends with a plain `OperationCanceledException`." **Code:** (1) `SqlFakeBank.cs:72` and `:114`: `try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { throw new SimulatedNoAnswerException(ct); }`. Both hangs only ever run under steering. (2) `PaymentService.cs:419`: `catch (OperationCanceledException e) when (budget.IsCancellationRequested) { return (new BankOutcome.Unknown(e is SimulatedNoAnswerException), null); }`. (3) `:501`: pass `outcome.Simulated` as the event's `simulated` argument. The `waitExpired` path (`:248`) stays `Unknown(false)`, because no bank was asked. (4) `PaymentDtoMapper` record: `Simulated = r.Payment.Settlement?.Simulated ?? r.Events.Any(e => e.Simulated)`. (5) TestKit `FakeBank.Hang(...)` / `HangStatus()` throw `SimulatedNoAnswerException` (see F6). | New integration test in `tests/CardLab.Integration.Tests/Payments/StatusCheckTests.cs`, `Steered_no_answer_is_recorded_as_simulated`. Step 1: steer `timeout_not_received`, then confirm. `GET /api/cards/{id}/payments/{ref}` must show the last event `bank_unanswered` with `simulated == true` and `payment.simulated == true`. Step 2: steer `does_not_answer`, then check. The last event must be `status_check_unanswered` with `simulated == true`. Step 3: an unsteered budget expiry (`ApiFactoryWithShortBankTimeout` plus a debit held by the applock, or the unit test `Bank_call_is_bounded_by_the_budget`) must still show `simulated == false`. **Mutation:** revert `:421` to `Unknown(false)`; the new test must fail. |
| F2 | major | `upstream:requirements` (R-15 forbids balance edits) | `src/CardLab.Infrastructure/Sql/DatabaseProbe.cs:66-76` (the only refill is `TestDataReset`, never on `demo`); `tests/e2e/src/smoke/pay.spec.ts:18-20`; requirements R-15, §9; design DEC-14, §17 | **The demo's fake bank can only drain, and the deploy smoke depends on mutable demo state.** On `demo` the £25,000.00 opening balance falls with every completed payment. Each deploy smoke pays at least £1.00 plus whatever Ops van owes. Nothing can top the balance up: R-15 says the panel changes "no ledger, balance or record edits", and the reset is off on `demo` (AC-37). One visitor can simulate a £24,999 purchase and pay it, and from then on every demo payment fails with W-3. The smoke would also go red if a visitor froze Ops van or set a Fuel limit, because `simulatePurchase` asserts `approved`. | G-1 ("pay the full balance end to end in the deployed environment") and AC-26 (smoke ≤ 3 s on `demo`) stop working, permanently, with no product defect behind them. Deploy runs go red, and the fix would need a manual database edit, which CD-5/CD-8 make awkward. | Human decision. (a) Recommended: amend R-15 so the panel gains a labelled *Fake bank: restore opening balance* action. It posts a simulated credit `BankEntry` (never edits or deletes rows), and the smoke calls it first. (b) Keep R-15. The smoke reads `GET /api/fake-bank` and fails fast with "demo fake bank drained (£x)", and §17 plus a runbook record the manual remedy. (c) Accept and record in design §17 only. For any option, the smoke should also unfreeze Ops van and remove its Fuel/day limit through the admin API before it simulates the purchase, or use a merchant category no visitor-set limit can block. | (a): new integration test: top-up then pay £30,000 → completed; `BankEntry` rows only grow. (b): set the demo-equivalent local bank to £0.50 (`UPDATE BankAccount`) and run the smoke against local; it must fail with the drained message, not a W-3 regex mismatch. (c): `grep -n "drain" docs/features/002-full-balance-payment/02-tech-design.md` hits the §17 row. |
| F3 | minor | doc correction | `02-tech-design.md:341` ("heading gains 'Balance owed £x'"), `prs/PR-07.md:39`, `prs/PR-08.md:48`; code `web/src/pages/CardDetailPage.tsx:145` (`<span>` in the `<header>`) | The admin balance is a `<span>` next to the `h1`, not part of a heading as the design and briefs say. T-E-6 works around the mismatch with an exact-text match. | The design should describe what shipped. Keep the code: folding the figure into the `h1` would change the accessible name "Ops van" that 001 page objects and AC-1 rely on, and in reading order the span is already announced right after the heading. | Change the three docs to "the page header shows 'Balance owed £x' after the status badge (text, not part of the `h1`)". | `grep -n "heading gains" docs/features/002-full-balance-payment/02-tech-design.md docs/features/002-full-balance-payment/prs/PR-0[78].md` prints nothing. |
| F4 | minor | code (doc comment) | `src/CardLab.Domain/Ports/ICardStore.cs:87` | `ListActivityAsync` states the order but not the shown time. Since the BUG-009 erratum, a purchase is shown at `OccurredAt`, the processor time, while ordering stays by `DecidedAtUtc`. Folded in from the PR-07f nit. | The port is the contract that both stores and future slices implement. The BUG-009 regression came from exactly this ambiguity. | Summary: "Purchases ordered by `DecidedAtUtc` and shown at their processor time `OccurredAt` (001 AC-38, BUG-009); payments ordered and shown at `CreatedAtUtc` (confirm time); merged newest first (R-13, DEC-12)." | `grep -n "OccurredAt" src/CardLab.Domain/Ports/ICardStore.cs` hits line 87. |
| F5 | minor | doc correction | `02-design-digest.md:36` and `02-tech-design.md:160` ("one E2E spec waits"), `02-tech-design.md:735` (risk row "one spec covers the real wait"), `03-pr-plan.md:121` ("only T-E-3 waits"), `docs/kb/decisions/ADR-007-fake-bank-shipped-adapter.md:23`; `02-tech-design.md:526` (`PanScanner` "8+ digit runs"; the code scans for the literal account number, `tests/CardLab.TestKit/PanScanner.cs:28-33`); `02-tech-design.md:571` ("T-I-14 asserts … p95", but the perf test is T-I-17); `docs/kb/decisions/ADR-006-payment-three-step-flow.md` (no BUG-002 unlinked-budget or BUG-003 check-waits-for-in-flight wording); `docs/kb/index.md:6` (phase "(plan)"); `03-pr-plan.md:4` (`status: draft`) | Doc drift after the PR-08 F8 correction and the errata. The correction updated three places in the design but missed the digest, the DEC-6 "give up" line, the §17 risk row, the plan and ADR-007. The ADRs are what later slices read. | The digest and ADRs are the only design text downstream agents load (CONVENTIONS). Stale statements there will mislead slice 2 planning and the validator. | Replace each with the shipped fact: four real 10 s waits (T-E-3 ×2, T-E-5, T-E-6); PanScanner = 16+ digit runs plus the literal seeded account number; T-I-17 for p95. In ADR-006 add one sentence: "the bank budget is its own token, not linked to the request (a disconnect never cancels a debit); a status check awaits an in-flight confirm on this host within its own budget and never asks the bank meanwhile". Set the KB index phase to `build` and the plan to `status: approved`. | `grep -rn -i "one E2E spec\|one spec covers\|only T-E-3\|8+ digit\|T-I-14 asserts" docs/features/002-full-balance-payment docs/kb/decisions` prints nothing; `grep -n "not linked" docs/kb/decisions/ADR-006-payment-three-step-flow.md` hits. |
| F6 | minor | code + test-harness | `src/CardLab.Domain/Application/InFlightPayments.cs:21-37`, `:80-106` (the `GetOrAdd` factory with `RunAsync` wrapper and an identity-guarded `Remove`, *plus* an unconditional `Release` in `Complete`/`Fail`); `PaymentService.cs:366` (`_ = _inFlight.GetOrAdd(...)` discards the return, PR-04 nit 1); `PaymentService.cs:144` (the `PaymentConfirmAttempt` log runs outside the `try` that faults the registry, PR-04f residual); `tests/CardLab.TestKit/Fakes/FakeBank.cs:14-15`, `:218`, `:254` (`Hang()` throws an OCE at once, not via the caller's token) | (a) The registry is a general "start a task" API wrapped around a task that already exists, with two removal paths. That is speculative generality on the tier-3 money path, and the two open review nits live in it. If anything throws between lock #1 and the `try` at `:172`, the entry is never released: joiners wait forever, and every later check waits 10 s and answers W-10 without ever asking the bank. (b) The unit fake's `Hang()` reaches the service as an *adapter failure* (`Detail = "OperationCanceledException"`), not as the budget firing, despite its doc comment. Unit timeout tests therefore exercise the wrong branch. Only `Bank_call_is_bounded_by_the_budget` covers the real one. | A stuck in-flight entry has no recovery short of a restart. A fake that disagrees with the port contract (§5: "throws `OperationCanceledException` when the caller's `ct` fires") lets branch-level mutations through. | (a) Replace `GetOrAdd`/`RunAsync`/`Remove` with `void Register(PaymentReference, Task<Receipt>)` (throws `InvalidOperationException` if already registered) plus the existing `Complete`/`Fail`/`TryGet`. Move the Create-path `PaymentConfirmAttempt` log inside the `try` at `:172`, or open that `try` before `:144`. (b) `FakeBank.Hang`/`HangStatus`: `await Task.Delay(Timeout.Infinite, ct)` and then throw `SimulatedNoAnswerException` (F1). Unit tests that hang build the fixture with `bankTimeoutSeconds: 0`, as `Bank_call_is_bounded_by_the_budget` does. Fix the doc comment. | (a) `InFlightPaymentsTests`: `Register` twice for one reference throws. Mutation: drop `Release` from `Complete`, and the BUG-006 test still fails 3/3. (b) Mutation: delete the `when (budget.IsCancellationRequested)` filter at `PaymentService.cs:419`. At least one hang-based unit test must fail; today none does. |
| F7 | minor | code (test) | design §4.1 rule "cardholder pages also usable at 768 px", checked by "E2E viewport check" (02-tech-design.md:362); no `setViewportSize` under `tests/e2e/src/payments/` | The 768 px rule has no test. | NFR-5 (should). The layout is a fixed 720 px column, so a regression (for example a wide table without `overflow-x`) would ship unnoticed. | In `tests/e2e/src/payments/a11y.spec.ts`, add a pass at `page.setViewportSize({ width: 768, height: 1024 })` over view, review and receipt that asserts `await page.evaluate(() => document.documentElement.scrollWidth) <= 768`. | Mutation: `.page { min-width: 900px; }` in `web/src/pages/cardholder/Cardholder.module.css` fails the new pass. |
| F8 | minor | code (dead code) | `web/src/notImplemented.ts`, `tests/e2e/src/notImplemented.ts` (skeleton helpers, zero importers); `web/src/api/apiClient.ts:49`, `httpApiClient.ts:63`, `fakeApiClient.ts:313` `listTransactions` (no page calls it since DEC-12 moved the admin table to `listActivity`) | Skeleton leftovers and a client method with no caller. | Dead code in the SPA port drifts from the server, and it invites the next coder to use the 001 list instead of activity. | Delete both `notImplemented.ts` files. Remove `listTransactions` from the SPA `ApiClient`, the HTTP client and the fake, together with their tests. The server endpoint `/transactions` stays (DEC-12, processor-facing). | `git grep -n "notImplemented\|listTransactions" -- web/src tests/e2e/src` prints nothing; `npm run typecheck --prefix web` and `npm test --prefix web -- --run` stay green. |
| F9 | minor | code | `src/CardLab.Domain/Application/PaymentService.cs:246-249` with `LogSettled` (`:529`) | When the check's wait for an in-flight confirm expires (BUG-003), the service logs `BankCallUnanswered(call: "status")` even though no bank call was made. | §12 defines the event as "the bank did not answer". An operator reading logs would chase a bank fault that never happened. | When `waitExpired`, skip `BankCallUnanswered`: pass a flag into `LogSettled`, or log a distinct `detail` such as `in_flight_wait_expired` on the `StatusCheckUnanswered` event. | Extend `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget`: `Assert.Empty(f.Logger.Events("BankCallUnanswered").Where(e => (string)e.Fields["Call"] == "status"))`. |

Nits (no action required): `web/src/components/CardholderShell.tsx:22-25` calls the admin `listCards()` for a name the view
already returns (`card.cardholderName`) · `ListRecentEntriesAsync`, `ListPaymentRecordsAsync` and `ListActivityAsync` still
have no tie-breaker for equal times, and `BankSteering` has no `CHECK (Id = 1)` (PR-05 nits) · T-E-1's time regex cannot
tell London from UTC in November (PR-08 nit) · `bugs.md` rows BUG-002/003/004/005/007/008 stay `fixed` until the validator
moves them to `verified`.

## Money invariants: checked and holding
- **Never paid twice:** `Payment.Reference` is unique, the quote PK is unique, and `ConfirmRules.Decide` puts *Repeat* first
  (`ConfirmRules.cs:57`). A repeated confirm takes the lock, sees the payment, records `ConfirmRepeated` and joins or returns
  the stored receipt. Covered by T-P-2 (1,000 scripts), T-I-4, T-I-5 (20 parallel confirms, exactly one `Payment` and one
  `BankEntry`), and the fake bank's idempotency on reference (contract T-U-24 / T-I-15).
- **One in flight:** `GetPendingPaymentAsync` runs inside lock #1, backed by `UX_Payment_OnePending` (migration `:261-266`).
  Covered by T-P-3 and contract P-cases.
- **Never half-applied:** the bank is called only between lock #1 and lock #2, never inside either (`PaymentService.cs:172-185`, bank call at `:176`).
  Settlement re-reads state under the lock and is a no-op once settled (`:491-494`). A lock #2 timeout leaves the payment
  Pending with `settle_lock_timeout`, and the status check settles it by reference. T-I-8 reads both sides after every step
  of J-1/J-3 under all five steering values.
- **Bank budget not linked to the request token (BUG-002):** `AskBankAsync` uses `new CancellationTokenSource(...)`
  (`:414`), and steps 2 and 3 use `CancellationToken.None`. Covered by `Client_disconnect_never_cancels_the_debit_and_the_payment_settles`.
- **In-flight registry (DEC-5, BUG-003, BUG-006):** registration happens inside lock #1 before commit (`:366`), and release
  happens before completion (`InFlightPayments.cs:56-76`). A check awaits an in-flight confirm within its own budget and never
  asks the bank meanwhile (`:214-237`). See F6 for the remaining cleanup.
- **Balance arithmetic:** `Money.-` floors at zero, so `BalanceRules.Owed` floors too. Settlement computes before = owed in
  the lock and after = before − amount for completed, before for failed (`:505`). Covered by AC-36 (T-U-21, T-I-7) and T-P-1.
- **Wording v1 verbatim and stored:** `WordingV1.cs` matches §17 character for character (scripted diff in the PR-01 review;
  T-U-5, T-U-32). The review lines are stored in `PaymentQuote.RenderedLines` with `WordingVersion`, and the record page shows
  them. T-I-10 and T-E-6 compare the six W-1 lines literally.
- **Append-only, 6-year intent:** the only delete path is `TestDataReset` (`DatabaseProbe.cs:69-80`), which is mapped only when
  `CardLab:TestEndpoints=true` (`appsettings.json:12` false; AC-37 / T-I-11). All payment FKs are `Restrict`. Only the quote's
  status/refusal columns and a pending payment's settlement columns are ever updated (`SqlPaymentWriter.cs`).
- **Privacy:** the full account number is used only to derive the last four in the seed (`DatabaseSetup.cs:155`). Logs carry
  the last four only (`CardLabEvents`). T-I-13 scans responses, logs and every stored row; T-I-14 scans logs; T-E-4 scans page
  text.

## Compliance pre-read decisions
| CD | Decision | Honoured by | Status |
|---|---|---|---|
| CD-1 | strictest reading, one behaviour for all | no cohort branching anywhere | ✔ |
| CD-2 | £0.00 fee with words, total, pending-true time | W-1 (`WordingV1.ReviewScreen`), review page, T-U-5, T-E-1 | ✔ |
| CD-3 | plain-language outcome template | W-2..W-10 verbatim, T-U-32, T-E-1/2/3 | ✔ |
| CD-4 | fields + verbatim text + version | `PaymentQuote` columns, record DTO/page, T-I-10, T-E-6 | ✔ |
| CD-5 | 6 years from final state, no product delete path | no delete path except the gated reset; `Restrict` FKs | ✔ (intent; no time-based purge exists) |
| CD-6 | check + auto check on open | `CheckStatusAsync`; SPA auto-check on the view and admin detail (DEC-13); T-E-3, T-E-6 | ✔ |
| CD-7 | screen + record approved as the pattern | one review screen, record shape reusable (`Fee`, `Kind`, before/after) | ✔ |
| CD-8 | refused refs 6 years from refusal; pending kept until settled | `RefusedAtUtc` stored; nothing deletes pending | ✔ |
| CQ-8 / decision 23 | steered outcomes marked simulated | refusals, completions and recorded not-received: ✔; **steered no-answer: ✘ (F1)** | ✘ |

## Cross-PR seams
| Seam | Status |
|---|---|
| `ICardStore`/`IPaymentWriter` ↔ `SqlCardStore`/`SqlPaymentWriter` ↔ `InMemoryCardStore` | one `PaymentStoreContractTests` run against both (T-U-23, T-I-16), incl. BUG-009 shown-time case ✔ |
| `ICurrentAccountBank` ↔ `SqlFakeBank` ↔ TestKit `FakeBank` | `CurrentAccountBankContractTests` on both (T-U-24, T-I-15) ✔; hang semantics differ (F6b), simulated no-answer missing (F1) |
| API DTOs (`PaymentContracts.cs`) ↔ `web/src/api/types.ts` | field-by-field match, wire names identical ✔ |
| SPA `FakeApiClient` ↔ real API | consistent after BUG-007 (PR-03f) and BUG-009 (PR-07f) ✔; differs on the simulated flag for no-answer events (F1) |
| SPA ↔ API routes | `/cardholder/:id`, `/pay`, `/payments/:reference`, admin record route ✔; E2E 30/30 on two browsers |
| Seed constants `appsettings.json` ↔ `Seed.cs` ↔ `seed.ts` | identical (name, 51907731/7731, GUID, 25000.00, authority text) ✔ |
| DEC-2 fake-bank boundary | `SqlFakeBank` touches only `Bank*` tables; card store never reads them ✔ |
| Ad-hoc harness duplicates | none left; the remaining web test subclasses (`SlowCheckFake`, `GatedCardClient`, `RecordFake`) only gate or pin fixtures ✔ |

## Traceability (gaps in **bold**)
| Req | Code | Tests (that would fail without it) | Status |
|---|---|---|---|
| R-1 / AC-1, AC-3 | `BalanceRules.Owed`, `SqlCardStore.GetBalanceAsync`, `CardEndpoints.GetCard` `balanceOwed` | T-U-1, T-U-2, T-P-1, T-I-1, T-I-19 | ✔ |
| AC-2 | `ListActivityAsync`, `ActivitySection` | T-I-2, T-I-19, T-W-8, T-E-6 | ✔ |
| R-2, R-16 / AC-4 | `LinkedAccount` seed, `CardholderViewService`, DEC-9 actor | T-I-1, T-E-1, T-I-13 (actor) | ✔ |
| R-3 / AC-5, AC-24 | `BalanceRules.Availability`, W-7/W-8 | T-U-3, T-U-4, T-W-2, T-E-2 | ✔ |
| R-4 / AC-6 | `QuoteRules`, `WordingV1.ReviewScreen`, `ReviewPage` | T-U-5, T-U-9, T-W-3, T-E-1 | ✔ |
| R-5, R-7, R-12 / AC-7 | `PaymentService.ConfirmAsync`, `ReceiptPage` | T-U-10, T-I-2, T-W-4, T-E-1 | ✔ |
| R-5 / AC-8, AC-30 | `ConfirmRules` refuse paths, `SetQuoteStatus(Refused)` | T-U-11, T-U-19, T-U-33, T-I-3, T-W-3, T-E-2 | ✔ |
| R-7 / AC-9, AC-10 | *Repeat* first, `InFlightPayments` | T-U-12, T-U-13, T-I-4, T-I-5 (exact equality after BUG-008) | ✔ |
| R-8 / AC-11 | pending check in lock + `UX_Payment_OnePending` | T-U-6, T-U-14, T-P-3, T-E-3 | ✔ |
| R-10 / AC-12, AC-31, AC-35 | `BankOutcome.Refused` → Failed, W-3/W-4 | T-U-15, T-U-20, T-I-2, T-I-15, T-E-2 | ✔ |
| R-11 / AC-13, AC-32 | budget CTS, pending + W-5 | T-I-6, T-E-3 (≤ 12 s, real 10 s) | ✔ |
| R-11 / AC-14..AC-16, AC-34, AC-38 | `CheckStatusAsync`, SPA auto-check, `EffectiveAt = DebitedAt ?? now` | T-U-16..T-U-18, T-U-22, T-I-7, T-E-3, T-E-6 | ✔ |
| R-6 / AC-17 | `PaymentStateMachine` | T-U-7, T-P-4 | ✔ |
| R-9 / AC-18 | three-step flow | T-I-8 | ✔ |
| R-13 / AC-19 | spend sums only `Transaction` | T-I-9 | ✔ |
| R-14 / AC-20 | `PaymentQuote`/`Payment`/`PaymentEvent`, record DTO + page | T-I-10, T-W-6, T-E-6 | **✘ for the simulated flag on steered no-answer (F1)** |
| R-15 / AC-21, AC-37 | `TestDataReset`, gated mapping | T-I-11 | ✔ |
| R-15 / AC-22 | persisted `Bank*` tables | T-I-12 | ✔ |
| R-15 / AC-33 | panel only on the view; simulated flag | T-E-4, T-W-5 | ✔ for refusals; **✘ for no-answer outcomes (F1)** |
| R-15 (demo operation) | no refill path on `demo` | none | **gap (F2)** |
| R-16, NFR-3 / AC-23 | actor from card, last-four only | T-I-13, T-E-4 | ✔ |
| R-17 / AC-24 | `MoneyBounds` in `Availability` | T-U-4, T-W-2 | ✔ |
| R-1, R-11, R-14 / AC-36 | before/after in settlement | T-U-21, T-I-7 | ✔ |
| NFR-1 / AC-25 | rules in `CardLab.Domain.Payments` | T-P-1..T-P-4 | ✔ |
| NFR-2 / AC-26 | two short locks; view p95 | T-I-17 (local p95), T-E-7 (≤ 3 s on `demo`, runs at deploy) | ✔ (demo run is the validator's) |
| NFR-4 / AC-27 | `CardLabEvents` 1003-1005 | T-I-14 | ✔ (F9 log accuracy) |
| NFR-5 / AC-28 | §4.1 rules, `aria-disabled` guard | T-E-5 (axe + keyboard, both browsers), T-W-3 | ✔; **768 px rule untested (F7)** |
| NFR-6 | no new deps/resources | diff: no package or Bicep changes | ✔ |
| NFR-7 / AC-29 | §17 verbatim | T-C-1 (validator) | validator |
| DEC-14/15 origin | header literal → enum; badges | T-U-35, T-I-18, T-E-7 | ✔ |
| Code without a requirement | none found. `ActivityEntry` category/reason/origin are additive fields that keep 001's columns (DEC-12), and the receipt GET is §7 | | ✔ |

## Routing for the orchestrator
- **To the human via `/sdlc:bugs`:**
  - **F1** (`upstream:design`): the §5 port contract erratum (how a steered no-answer is signalled), then a small code fix
    brief. **Recheck needed** only if the chosen mechanism differs from the one proposed here. Otherwise the orchestrator
    verifies the new test and the mutation.
  - **F2** (`upstream:requirements`): choose option a, b or c.
- **Code-only, one small fix PR:** F4, F6, F7, F8, F9, plus the F1 code once the erratum lands. Each has a mechanical check.
- **Doc-only (orchestrator):** F3, F5.
