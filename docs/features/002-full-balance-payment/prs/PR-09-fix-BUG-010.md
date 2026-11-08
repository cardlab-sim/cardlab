---
pr: PR-09
feature: 002-full-balance-payment
title: "Feature review fixes: steered no-answer marked simulated (BUG-010), in-flight registry, 768 px, dead code, log accuracy"
wave: 5
depends_on: [PR-08, PR-03f, PR-07f]
branch: pr/002-full-balance-payment/09-fix-feature-review
base: feat/002-full-balance-payment
---

# PR-09: Feature review fixes (BUG-010 + F4, F6, F7, F8, F9)

Source: `reviews/feature.review.md` (head aa4c956). Each finding's row there gives the full evidence and the check. Read
F1, F4, F6, F7, F8 and F9 before you start.

## Goal
1. **BUG-010 (F1, S1, human decision option A).** A steered no-answer must be recorded as simulated. Today the fake bank's
   steered hang ends in a plain `OperationCanceledException`, so `PaymentService.AskBankAsync` returns `Unknown(false)` and
   the `BankUnanswered` / `StatusCheckUnanswered` event is appended with `simulated: false`. The design erratum has already
   landed (02-tech-design.md §5 `ICurrentAccountBank` contract, DEC-7, §9, §15): *a no-answer produced by steering ends,
   when the caller's `ct` fires, with `SimulatedNoAnswerException : OperationCanceledException` (`CardLab.Domain.Ports`); any
   other no-answer ends with a plain `OperationCanceledException`.*
   - Add `public sealed class SimulatedNoAnswerException : OperationCanceledException` in `CardLab.Domain.Ports`
     (`ICurrentAccountBank.cs`), with a constructor taking the `CancellationToken`. Update the port's doc comment.
   - `SqlFakeBank.RequestDebitAsync` and `GetDebitStatusAsync`: wrap each hang:
     `try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { throw new SimulatedNoAnswerException(ct); }`.
     Both hangs run only under steering.
   - `PaymentService.AskBankAsync`: `catch (OperationCanceledException e) when (budget.IsCancellationRequested)` →
     `Unknown(e is SimulatedNoAnswerException)`.
   - `PaymentService` settle path (the `transition.To == PaymentState.Pending` branch): pass `outcome.Simulated` as the
     event's `simulated` argument. The `waitExpired` path stays `Unknown(false)`, because no bank was asked.
   - `PaymentDtoMapper` record: `Simulated = r.Payment.Settlement?.Simulated ?? r.Events.Any(e => e.Simulated)`.
   - TestKit `FakeBank.Hang(...)` / `HangStatus()` (see 3b) end with `SimulatedNoAnswerException`.
   - The SPA `FakeApiClient` already marks these events simulated. Check that `payment.simulated` on a pending steered
     record matches the real API (`fakeApiClient.ts` around :494 and :509-511) and fix only if it differs.
2. **F4.** `ICardStore.ListActivityAsync` summary (`src/CardLab.Domain/Ports/ICardStore.cs:87`): "Purchases ordered by
   `DecidedAtUtc` and shown at their processor time `OccurredAt` (001 AC-38, BUG-009); payments ordered and shown at
   `CreatedAtUtc` (confirm time); merged newest first (R-13, DEC-12)."
3. **F6.**
   (a) `InFlightPayments`: replace `GetOrAdd` / `RunAsync` / identity-guarded `Remove` with
   `void Register(PaymentReference, Task<Receipt>)`, which throws `InvalidOperationException` if the reference is already
   registered. Keep `Complete` / `Fail` / `TryGet` and the DEC-5 / BUG-006 guarantees: registered inside lock #1 before
   commit, released before completion is observable, and joiners get the same outcome. In `PaymentService`, stop
   discarding the result (`:366`), and make sure nothing that can throw runs between registration and the `try` that
   releases it. Move the Create-path `PaymentConfirmAttempt` log (`:144`) inside that `try`, or open the `try` earlier.
   (b) TestKit `FakeBank.Hang` / `HangStatus`: `await Task.Delay(Timeout.Infinite, ct)`, then throw
   `SimulatedNoAnswerException(ct)`, matching the port contract. Unit tests that hang build the fixture with
   `bankTimeoutSeconds: 0`, as `Bank_call_is_bounded_by_the_budget` does. Fix the PLANNER-DECISION doc comment (`:14`) and
   the method comments (`:116`, `:146`).
4. **F7.** In `tests/e2e/src/payments/a11y.spec.ts`, add a pass at `page.setViewportSize({ width: 768, height: 1024 })`
   over view, review and receipt. It asserts `document.documentElement.scrollWidth <= 768` (design §4.1, NFR-5).
5. **F8.** Delete `web/src/notImplemented.ts` and `tests/e2e/src/notImplemented.ts`. Remove `listTransactions` from the SPA
   `ApiClient` (`apiClient.ts`), `httpApiClient.ts` and `fakeApiClient.ts`, together with their tests. The server endpoint
   `/transactions` stays (DEC-12).
6. **F9.** When the check's wait on an in-flight confirm expires (`waitExpired`, `PaymentService.cs:246-249`), do not log
   `BankCallUnanswered(call: "status")`, because no bank was asked. Pass a flag into `LogSettled` (or log
   `detail: "in_flight_wait_expired"` on the `StatusCheckUnanswered` event) and skip `BankCallUnanswered` there.

## Acceptance criteria covered
| AC | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-20 | R-14 | any payment state, including pending | the record is opened | it shows whether the outcome was simulated |
| AC-33 | R-15 | an outcome produced under steering (including no answer) | it is recorded | it is marked simulated |
| AC-10 | R-7 | two confirms of one reference at the same instant | both run | both callers receive the same outcome (registry refactor, no behaviour change) |
| NFR-5 / AC-28 | §4.1 | cardholder pages | shown at 768 px | no horizontal scroll |

## Files you own (edit only these)
| File | What to do |
|------|------------|
| src/CardLab.Domain/Ports/ICurrentAccountBank.cs | add `SimulatedNoAnswerException`; contract doc comment |
| src/CardLab.Domain/Ports/ICardStore.cs | F4 doc comment |
| src/CardLab.Domain/Application/PaymentService.cs | F1 mapping + event flag; F6a registration/try; F9 log |
| src/CardLab.Domain/Application/InFlightPayments.cs | F6a `Register` API |
| src/CardLab.Infrastructure/FakeBank/SqlFakeBank.cs | F1 throw on steered hangs |
| src/CardLab.Api/Contracts/PaymentDtoMapper.cs | F1 record `Simulated` fallback |
| tests/CardLab.TestKit/Fakes/FakeBank.cs | F6b hang semantics + docs |
| tests/CardLab.TestKit/Contracts/CurrentAccountBankContractTests.cs | only if a contract case is needed for the new exception |
| tests/CardLab.Domain.Tests/Application/PaymentServiceTests.cs | fixtures with `bankTimeoutSeconds: 0` for hangs; F1 unit test |
| tests/CardLab.Domain.Tests/Application/PaymentStatusCheckTests.cs | same; F9 assertion |
| tests/CardLab.Domain.Tests/Application/InFlightPaymentsTests.cs | F6a tests |
| tests/CardLab.Domain.Tests/Harness/PaymentHarnessTests.cs | F6b: hanging calls get a short-lived token (e.g. 50 ms CTS) and assert `SimulatedNoAnswerException` (T-U-37, `FakeBank_scripts_do_not_touch_a_reference_the_bank_has_seen`) |
| tests/CardLab.Domain.Tests/Application/CardholderViewServiceTests.cs | F6b: the `Hang()` case builds the fixture with `bankTimeoutSeconds: 0` |
| tests/CardLab.Integration.Tests/Payments/StatusCheckTests.cs | F1 integration test |
| tests/e2e/src/payments/a11y.spec.ts | F7 |
| web/src/notImplemented.ts, tests/e2e/src/notImplemented.ts | F8 delete |
| web/src/api/apiClient.ts, web/src/api/httpApiClient.ts, web/src/api/fakeApiClient.ts, web/src/api/httpApiClient.test.ts, web/src/api/fakeApiClient.test.ts | F8 remove `listTransactions`; F1 fake check |

If you need another file, STOP and report it (BLOCKED) rather than editing it.

## Tests (write first; see each RED before the fix)
| # | Test | File | Then | Catches |
|---|------|------|------|---------|
| 1 | new `Steered_no_answer_is_recorded_as_simulated` | Integration `StatusCheckTests.cs` | (1) steer `timeout_not_received`, confirm: last event `bank_unanswered` with `simulated == true` and `payment.simulated == true` on `GET /api/cards/{id}/payments/{ref}`; (2) steer `does_not_answer`, check: last event `status_check_unanswered` with `simulated == true`; (3) an unsteered budget expiry still shows `simulated == false` (use `ApiFactoryWithShortBankTimeout` with a debit held, or cover (3) by the unit test `Bank_call_is_bounded_by_the_budget` extended to assert `simulated == false`) | BUG-010 |
| 2 | new unit: steered hang → event simulated; unsteered budget → not simulated | `PaymentServiceTests.cs` / `PaymentStatusCheckTests.cs` | as named | BUG-010 at unit level |
| 3 | new `Register_twice_for_one_reference_throws` | `InFlightPaymentsTests.cs` | `InvalidOperationException` | F6a |
| 4 | extend `Check_wait_on_an_in_flight_confirm_is_bounded_by_the_check_budget` | `PaymentStatusCheckTests.cs` | `Assert.Empty(f.Logger.Events("BankCallUnanswered").Where(e => (string)e.Fields["Call"] == "status"))` | F9 |
| 5 | new 768 px pass | `a11y.spec.ts` | `scrollWidth <= 768` on view, review, receipt (both browsers) | F7 |
| 6 | existing suites | all | still green (BUG-006 disconnect test, T-U-13, T-I-4, T-I-5, contract suites on both fakes) | regressions |

## Mutations to report (each must make a test fail; revert after)
- `AskBankAsync`: revert to `Unknown(false)` → test 1 (and 2) fail.
- `AskBankAsync`: delete the `when (budget.IsCancellationRequested)` filter → at least one hang-based unit test fails (today none does).
- `InFlightPayments.Complete`: drop the release → the BUG-006 test fails 3/3.
- `web/src/pages/cardholder/Cardholder.module.css`: `.page { min-width: 900px; }` → test 5 fails.

## Checks (from the review)
- `git grep -n "notImplemented\|listTransactions" -- web/src tests/e2e/src` prints nothing.
- `grep -n "OccurredAt" src/CardLab.Domain/Ports/ICardStore.cs` hits the `ListActivityAsync` summary.

## Gate
`commands` from docs/sdlc/project.json: build (`-warnaserror`), unit, integration (LocalDB), web tests, lint, typecheck, and
E2E `--project local --project local-firefox`. PR title "CL-3 PR-09: <title>", body starts "Ticket: CL-3" and names BUG-010.
