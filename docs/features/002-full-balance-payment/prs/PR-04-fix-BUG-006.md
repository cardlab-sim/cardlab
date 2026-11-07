---
pr: PR-04f
feature: 002-full-balance-payment
title: "Fix BUG-006: in-flight registry entry cleared before the confirm returns"
wave: 3
depends_on: [PR-04]
branch: pr/002-full-balance-payment/04f-fix-bug-006
base: feat/002-full-balance-payment
---

# PR-04f: Fix BUG-006, in-flight registry entry cleared before the confirm returns

## Goal
`Client_disconnect_never_cancels_the_debit_and_the_payment_settles` (`tests/CardLab.Domain.Tests/Application/PaymentServiceTests.cs:365`)
fails intermittently on CI at line 382 (`Assert.False(f.InFlight.TryGet(...))`). Cause: `PaymentService.ConfirmAsync`
calls `completion.SetResult(result)` and returns; `InFlightPayments.RunAsync` awaits that task and removes the entry in a
continuation that runs later (`RunContinuationsAsynchronously` / `ConfigureAwait(false)`), so the registry can still hold
the reference after the confirm has returned. Production impact is nil (a check that finds the stale entry awaits an
already-completed task, then re-reads the stored state), but the feature CI is red and the DEC-5 contract ("cleared on
completion") should hold deterministically.

Make it deterministic: when `ConfirmAsync` (or a joiner) returns, the reference is no longer registered. Preferred: the
registry entry is removed synchronously before the confirm's completion is signalled (e.g. an `InFlightPayments`
operation the service calls before `SetResult` / `TrySetException`, or the registry removes the entry before completing its
own source with the result it was handed); keep the DEC-5 guarantees (registered before step 1 commits; a repeat always
finds the task while it runs; joiners get the same outcome). Do not weaken the test by polling.

## Acceptance criteria covered
| AC | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-10 | R-7 | two confirms of one reference at the same instant | both run | both callers receive the same outcome, and one payment exists |

(Regression only; behaviour unchanged.)

## Owned files
| File | Change |
|------|--------|
| src/CardLab.Domain/Application/InFlightPayments.cs | remove the entry before completion is observable |
| src/CardLab.Domain/Application/PaymentService.cs | call the removal at the right point if needed |
| tests/CardLab.Domain.Tests/Application/InFlightPaymentsTests.cs | regression test |
| tests/CardLab.Domain.Tests/Application/PaymentServiceTests.cs | only if a helper is needed; keep line-382 assertion |

## Tests
| # | Test | File | Then | Catches |
|---|------|------|------|---------|
| 1 | new: `Entry_is_gone_when_the_awaited_task_completes` | InFlightPaymentsTests.cs | a continuation on the returned task (run synchronously) already sees `TryGet == false` | removal racing the completion (BUG-006) |
| 2 | existing T-U-13 / F2 tests | PaymentServiceTests.cs, PaymentStatusCheckTests.cs | still green | DEC-5 regressions |

Write test 1 first and see it RED on the current code (if a single run is not reliably red, make it deterministic, e.g. a
`TaskContinuationOptions.ExecuteSynchronously` continuation that records `TryGet`). Then prove stability: run
`dotnet test tests/CardLab.Domain.Tests --filter "FullyQualifiedName~Client_disconnect_never_cancels|FullyQualifiedName~InFlightPayments"`
200 times in a loop (or an xUnit theory repeat), 0 failures, and report the count.

## Gate
`commands` from docs/sdlc/project.json: build, unit, lint, typecheck. PR title "CL-3 PR-04f: <title>", body starts "Ticket: CL-3".
