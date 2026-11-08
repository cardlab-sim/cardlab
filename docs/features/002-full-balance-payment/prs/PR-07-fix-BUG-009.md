---
pr: PR-07f
feature: 002-full-balance-payment
title: "Fix BUG-009: activity shows a purchase at its processor time, ordered by decision time (as 001)"
wave: 4
depends_on: [PR-07, PR-03f]
branch: pr/002-full-balance-payment/07f-fix-bug-009
base: feat/002-full-balance-payment
---

# PR-07f: Fix BUG-009, activity shows a purchase at its processor time (as 001)

## Goal
001's admin transaction history showed each purchase at its processor timestamp (`OccurredAt`, AC-38) and ordered the
list by decision time (`DecidedAtUtc`). 002's combined activity (`ICardStore.ListActivityAsync`, PR-07) sets
`ActivityEntry.At = DecidedAtUtc`, so a late-arriving purchase shows the time it arrived; 001's E2E
`tests/e2e/src/formatting.spec.ts` "T-E-6 limit and transaction time" fails on both browsers. Human decision (design §5
erratum): **show** a purchase at `OccurredAt`; **order** purchases by `DecidedAtUtc` and payments by confirm time
(`CreatedAtUtc`), newest first, merged. Payments' shown time is unchanged (confirm time). Apply the same rule to both
stores (SQL and in-memory) through the shared contract suite, and to the web fake client (`fakeApiClient.ts`), which
PR-03f had aligned to decision time for both showing and ordering.

## Acceptance criteria covered
| AC | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-2 | R-1 | a card with purchases and a completed payment | the admin opens the card | the activity lists purchases and payments newest first |

Plus 001 AC-38 (UK format of the processor's transaction time), restored.

## Files you own (edit only these)
| File | Status in skeleton | What to do |
|------|--------------------|------------|
| src/CardLab.Infrastructure/Sql/SqlCardStore.cs | complete (PR-05/PR-07) | `ListActivityAsync`: order by decision/confirm time, `At` = `OccurredAt` for purchases |
| src/CardLab.Domain/Payments/PaymentRecords.cs | complete | `ActivityEntry` doc comment: `At` is the shown time; order is by decision/confirm time |
| tests/CardLab.TestKit/Fakes/InMemoryCardStore.cs | complete (PR-02) | same rule in the in-memory `ListActivityAsync` |
| tests/CardLab.TestKit/Contracts/PaymentStoreContractTests.cs | complete (PR-02) | new contract case: a late purchase shows its processor time but is placed by decision time |
| tests/CardLab.Integration.Tests/Payments/AdminPaymentEndpointsTests.cs | complete (PR-07) | API-level assertion of the shown `at` for a backdated purchase |
| web/src/api/fakeApiClient.ts | complete (PR-03f) | purchase activity `at` = `occurredAt`, order by decision time |
| web/src/api/fakeApiClient.test.ts | complete (PR-03f) | rewrite the activity-order test for the rule above |
| web/src/pages/CardDetail.test.tsx | complete (PR-03f) | only if T-W-9 or fixtures need the new rule |

## Tests
| # | Test | File | Then | Catches |
|---|------|------|------|---------|
| 1 | new contract case `Late_purchase_shows_its_processor_time_ordered_by_decision` | PaymentStoreContractTests.cs (runs on in-memory and SQL) | a purchase decided after a payment but stamped before it is listed above the payment, with `At` = its stamp | showing decision time; ordering by stamp |
| 2 | API assertion | AdminPaymentEndpointsTests.cs | `GET /activity` `at` of a processor purchase stamped 2026-10-24T23:30:00Z is that instant | API shows decision time |
| 3 | fake order test | fakeApiClient.test.ts | the same rule in the web fake | fake drifting from the store |
| 4 | 001 E2E (read-only) | tests/e2e/src/formatting.spec.ts | passes on chromium and firefox | the BUG-009 regression |

Write tests 1-3 first and see them RED. Mutations: show `DecidedAtUtc` again → tests 1 and 2 fail; order by
`OccurredAt` → test 1 fails. Run the full E2E suite (`npm run e2e --prefix tests/e2e`); it must be fully green,
including 001's specs.

## Gate
`commands` from docs/sdlc/project.json: build, unit, lint, typecheck, plus integration and E2E. PR title
"CL-3 PR-07f: <title>", body starts "Ticket: CL-3".
