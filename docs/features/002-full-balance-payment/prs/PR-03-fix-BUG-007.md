---
pr: PR-03f
feature: 002-full-balance-payment
title: "Fix BUG-007: shared web fake client feeds simulated purchases into activity and balance"
wave: 4
depends_on: [PR-07]
branch: pr/002-full-balance-payment/03f-fix-bug-007
base: feat/002-full-balance-payment
---

# PR-03f: Fix BUG-007, shared web fake client feeds simulated purchases into activity and balance

## Goal
The shared `FakeApiClient` (`web/src/api/fakeApiClient.ts`, PR-03) does not feed `simulatePurchase` into `listActivity`
or the card's `balanceOwed`, and hard-codes `origin: 'cardholder_view'`. PR-07 therefore added a test-local
`ActivityFake` subclass in `web/src/pages/CardDetail.test.tsx` that duplicates part of the fake with inconsistent data
(T-W-8 asserts "Balance owed £20.00" beside £32.50 of approved purchases; rows concatenated, not ordered by time).
Make the shared fake one consistent model (R-1: owed = approved purchases − completed payments, floored at zero;
activity = purchases and payments newest first; origin settable per quote for DEC-15 smoke badges), then delete
`ActivityFake` / `paymentsOverride` and make the CardDetail tests use the shared fake with consistent numbers.

## Acceptance criteria covered
| AC | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-1 | R-1 | a card with approved purchases | the balance is shown | it is approved purchases minus completed payments |

(Test-harness fix; production code unchanged.)

## Files you own (edit only these)
| File | Status in skeleton | What to do |
|------|--------------------|------------|
| web/src/api/fakeApiClient.ts | complete (PR-03) | simulated purchases feed activity and balance; activity newest first; origin per quote |
| web/src/api/fakeApiClient.test.ts | complete (PR-03) | tests for the above (first, seen RED) |
| web/src/pages/CardDetail.test.tsx | complete (PR-07) | drop `ActivityFake` / `paymentsOverride`; consistent fixture numbers; keep every behaviour the tests pin |

## Tests
| # | Test | File | Then | Catches |
|---|------|------|------|---------|
| 1 | new: simulated purchase appears in activity and raises balance owed | fakeApiClient.test.ts | approved £12.50 after £150.50 → owed £163.00, activity newest first | fake drifting from R-1 |
| 2 | new: quote origin deploy_smoke shows on the record and payments list | fakeApiClient.test.ts | origin round-trips | hard-coded origin |
| 3 | existing T-W-6 / T-W-8 / 001 CardDetail tests | CardDetail.test.tsx | all green on the shared fake, numbers consistent | lost coverage when the local fake is removed |

Prove no coverage was lost: re-run PR-07's two review mutations (removing `checked.current.add` in
`CardDetailPage.tsx`; the post-check refetch guard) and confirm they still fail a test; revert.

## Gate
`commands` from docs/sdlc/project.json: build, unit, lint, typecheck. PR title "CL-3 PR-03f: <title>", body starts "Ticket: CL-3".
