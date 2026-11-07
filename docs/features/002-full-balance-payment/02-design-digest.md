---
feature: 002-full-balance-payment
source: ./02-tech-design.md
design_approved_at: 2026-11-07T15:34Z
risk_class: R2
ticket: CL-3
---

# Full balance payment (CL-3, slice 1): Design digest

Current design for downstream agents (planner, coders, reviewers, validator). Detail lives in
[02-tech-design.md](./02-tech-design.md) (cited as §n). Explainer:
[design/explainer.html](./design/explainer.html). Builds on the 001 design
([001 digest](../001-card-controls/02-design-digest.md)): same host, database, harness, UI rules.

## Shape
- A payments area inside the existing modular monolith (§1, §4): pure `CardLab.Domain.Payments`
  (balance, quote, confirm rules, state machine, wording v1); new port `ICurrentAccountBank` with
  one adapter, the persisted fake bank `CardLab.Infrastructure.FakeBank` (shipped to every
  environment incl. `demo`, with a steering control); payment tables in the same SQL database
  behind the existing `ICardStore` unit of work; cardholder endpoints under `/api/cardholder`;
  cardholder pages + admin payment record page in the existing SPA.
- No new hosted service, project, NuGet or npm dependency, Azure resource or secret (NFR-6).
- Money code only under `src/CardLab.Domain` (tier 3 review, property tests, NFR-1); no PAN or full
  account number anywhere (NFR-3); wording v1 verbatim (NFR-7); records never deleted by a product
  path (CD-5, CD-8); PR titles and bodies carry `CL-3`.

## Decisions (final)
| ID | Decision | Numbers / specifics it fixes |
|---|---|---|
| DEC-1 | Requirements decisions inherited as fixed | balance = approved purchases − completed payments, floored £0.00; balance changed → refuse + re-quote; one payment in flight per card; pending never re-sent; fee £0.00; no `AuditEvent` rows for payments; never call the bank inside the card lock |
| DEC-2 | Same three projects, new folders | `Domain/Payments`, `Infrastructure/Payments`, `Infrastructure/FakeBank`; fake-bank tables prefixed `Bank`, never joined with card tables (reviewer-enforced) |
| DEC-3 | Three-step confirm, lock only around local steps ([ADR-006](../../kb/decisions/ADR-006-payment-three-step-flow.md)) | step 1 locked: validate ref, compare balance, check no pending, insert `Payment(Pending)` + `ConfirmCreated`, commit; step 2 unlocked: `RequestDebitAsync`, 10 s budget; step 3 locked: settle Completed/Failed with balance before/after, or stay Pending + `BankUnanswered` |
| DEC-4 | Payments share the `ICardStore` unit of work | `ICardWriter.Payments : IPaymentWriter` inside the lock; reads on `ICardStore`; 001 interfaces untouched; `PaymentStoreContractTests` sibling suite |
| DEC-5 | Concurrent confirms of one reference join the in-flight call | singleton `InFlightPayments` (`ConcurrentDictionary<reference, Task<Receipt>>`); second confirm records `ConfirmRepeated`, awaits the task or returns the stored receipt; single-host assumption (ADR-001) |
| DEC-6 | Fake bank: own tables in the same DB/DbContext; timeout = never returns ([ADR-007](../../kb/decisions/ADR-007-fake-bank-shipped-adapter.md)) | "timeout, debit applied" writes the ledger then hangs; "timeout, not received" writes nothing then hangs; integration tests set `BankTimeoutSeconds=1`; one E2E spec waits the real 10 s |
| DEC-7 | Second one-shot steering for status checks | *Next status check: answers / does not answer* (default answers); unanswered → `StatusCheckUnanswered`, `Simulated = true` |
| DEC-8 | Reference format | `PAY-` + 8 Crockford base32 (regex `^PAY-[0-9A-HJKMNP-TV-Z]{8}$`), via `IPaymentReferences` (real: `RandomNumberGenerator`; fake: sequential `PAY-00000001`); collision → 503, re-review |
| DEC-9 | Cardholder actor derived from the card | `PaymentActor(Cardholder, cardId, card.CardholderName)`; `ICurrentActor` unchanged for admin actions |
| DEC-10 | Wording rendered by the server, stored verbatim | `WordingV1` renders W-1..W-10; API returns strings; SPA shows them verbatim; record stores them with `WordingVersion = "v1"` |
| DEC-11 | Cardholder UI: same SPA, three routes, single column | `/cardholder/:id`, `/cardholder/:id/pay`, `/cardholder/:id/payments/:reference`; max-width 720 px; fake-bank section last on the view, absent from review and receipt; admin card list gets a "Cardholder view" link |
| DEC-12 | Admin history via new `GET /api/cards/{id}/activity` | purchases + payments merged, newest first; `/transactions` unchanged; admin "Transactions" table becomes "Activity" |
| DEC-13 | Auto status check on open is triggered by the SPA | GET view is pure; SPA calls `POST .../check` when a pending payment is shown, then re-reads; admin card detail does the same |
| DEC-14 | Deploy smoke pays once on `demo` | `smoke/pay.spec.ts`: simulate purchase on "Ops van", steer fake bank to *complete*, pay full balance with header `X-CardLab-Origin: deploy-smoke`, assert W-2, receipt ≤ 3 s, record origin smoke |
| DEC-15 | `Origin` marker on the record (requirements decision 37, erratum) | `PaymentQuote.Origin varchar(20)`: enum name `CardholderView` (default) / `DeploySmoke`, wire `cardholder_view` / `deploy_smoke` (BUG-005) only when header value is exactly `deploy-smoke`; copied into `Receipt`, `PaymentRecord`, Payments list, `ActivityEntry`; "origin: smoke" badge |

## Interfaces (seams, §5)
Domain (`src/CardLab.Domain/Payments/`, no I/O):
- `enum PaymentState { Pending, Completed, Failed }`; `enum PaymentKind { Full }`; `enum FailureReason { InsufficientFunds, AccountUnavailable, NotReceived }`; `enum QuoteStatus { Open, Confirmed, Refused }`; `enum PayAvailability { Available, NothingToPay, AboveLimit, PaymentPending }`; `PaymentReference(string)` (`TryParse`); `PaymentId(Guid)`; `enum ActorKind { Admin, Cardholder }`; `PaymentActor(Kind, Id, Name)`.
- `BalanceRules.Owed(approvedPurchases, completedPayments)` (floors at zero); `BalanceRules.Availability(owed, hasPendingPayment)` (£0.00 → NothingToPay; > £1,000,000.00 → AboveLimit; pending → PaymentPending; else Available).
- `Quote(Reference, Card, Kind, Amount, Fee, Total, FromLabel, ToLabel, Actor, AuthorityBasis, CreatedAtUtc, Shown)`; `RenderedWording(Version, Heading, Lines, ButtonLabel)`; `QuoteRules.Create(...)` (Fee £0.00, Total = Amount).
- `ConfirmRules.Decide(quote, status, paymentForQuote, otherPending, currentOwed)` → `Create | Repeat | RefuseBalanceChanged(newOwed) | RefuseAlreadyRefused(newOwed) | RefusePendingExists(ref, amount)`.
- `PaymentStateMachine.Apply(current, BankOutcome) → Result<Transition, string>`; terminal states reject every outcome; `Unknown` leaves Pending.
- `BankOutcome(Simulated)`: `Completed(DebitedAtUtc?)`, `Refused(Reason)`, `NotReceived`, `Unknown`.
- `WordingV1`: `Version = "v1"`, `ReviewScreen` (W-1), `Completed` (W-2), W-3..W-10 helpers, `Label(name, lastFour)` → `"{name}, ending {lastFour}"`; `Money.ToDisplay()` → `£1,234.56` (new).

Ports:
- `ICurrentAccountBank`: `GetAccountAsync`, `ListRecentEntriesAsync(ref, take)`, `RequestDebitAsync(DebitRequest)`, `GetDebitStatusAsync(ref, reference)`. Contract: a seen reference is never applied twice; calls may never return (caller's `ct` is the budget → `OperationCanceledException`).
- `IPaymentReferences.Next()`; `IClock` (existing).
- `ICardStore` additions: `GetLinkedAccountAsync`, `GetBalanceAsync(CardId) → CardBalance`, `GetPaymentRecordAsync(reference)`, `ListPaymentRecordsAsync(CardId)`, `ListActivityAsync(CardId, take)` (newest first).
- `IPaymentWriter` (inside the lock): `SumApprovedPurchasesAsync`, `SumCompletedPaymentsAsync`, `GetQuoteAsync`, `GetPendingPaymentAsync`, `AppendQuote`, `SetQuoteStatus`, `CreatePayment` (Pending), `SettlePayment(id, Settlement)`, `AppendEvent(PaymentEvent)`; `PaymentEventKind` = ConfirmCreated, ConfirmRepeated, ConfirmRefused, DebitRequested, BankCompleted, BankRefused, BankUnanswered, StatusCheckCompleted/Refused/NotReceived/Unanswered.
- `PaymentRecord(Quote, Status, RefusalReason?, BalanceAtRefusal?, Payment?, Events)`.

Application (`CardLab.Domain.Application`):
- `PaymentService`: `QuoteAsync`, `ConfirmAsync`, `CheckStatusAsync`, `GetReceiptAsync` → `Result<Receipt|Quote, PaymentError>`; `Receipt(Reference, State, Amount, Fee, Time, FromLabel, ToLabel, Reason?, Message, BalanceOwed, Repeated)` + origin; `PaymentError` = CardNotFound, ReferenceNotFound, NothingToPay, AboveLimit, PaymentPending, BalanceChanged, QuoteRefused.
- `CardholderViewService.GetAsync(CardId) → CardholderView` (card, balance, availability + message, pending receipt, linked account + available balance, ≤ 10 card activity, ≤ 10 account entries).
- `InFlightPayments.GetOrAdd(reference, factory)`, `TryGet`, `Complete`/`Fail` (remove the entry before completing; BUG-006) singleton.

Infrastructure / host / web:
- `SqlPaymentWriter` (inside `SqlCardStore.WithCardLockAsync`); `SqlFakeBank : ICurrentAccountBank` with `IFakeBankSteering` (`Steering(NextDebitOutcome?, NextStatusCheck?)`; `NextDebitOutcome` = Complete, RefuseInsufficientFunds, RefuseAccountUnavailable, TimeoutDebitApplied, TimeoutNotReceived); `SystemPaymentReferences`; migration `AddPayments`; `Seeder` + `TestDataReset` extended.
- Api: `MapCardholderEndpoints`, `MapFakeBankEndpoints`, `MapCardEndpoints` additions.
- Web `ApiClient` gains `getCardholderView`, `createQuote`, `confirmPayment`, `checkPayment`, `getReceipt`, `getFakeBank`, `setFakeBank`, `listActivity`, `listPaymentRecords`, `getPaymentRecord`; `getCard` gains `balanceOwed`.

## Data (§6)
Migration `AddPayments` (additive, forward-only). Amounts `decimal(12,2)`, enums as short strings, times `datetime2(3)` UTC.
- `LinkedAccount` (unique per business; `LastFour` only, full number never stored; `AuthorityBasis`).
- `PaymentQuote` (PK `Reference varchar(12)`; shown-and-accepted columns immutable, incl. `Origin`; only `Status`, `RefusedAtUtc`, `RefusalReason`, `BalanceAtRefusal` change; index `(CardId, CreatedAtUtc DESC)`).
- `Payment` (unique `Reference`; filtered unique `UX_Payment_OnePending (CardId) WHERE State='Pending'`; `IX_Payment_Completed (CardId, State) INCLUDE (Amount)`; `BalanceBefore/After`, `Simulated`).
- `PaymentEvent` (append-only; index `(Reference, AtUtc)`).
- Fake bank: `BankAccount` (seeded £25,000.00), `BankDebitRequest` (idempotency memory), `BankEntry` (ledger, unique `Reference`), `BankSteering` (single row, read-and-clear).
- Seed: linked account "Brightline Supplies Ltd current account", last four `7731`, `BankAccountRef` `5a3e1d2c-7b4f-4c1a-9e2d-000000000731`; constants mirrored in `tests/CardLab.TestKit/Seed.cs`, `tests/e2e/src/seed.ts`.
- No delete path except `TestDataReset` (only when `CardLab:TestEndpoints=true`). Category spend still sums only `Transaction` rows.

## API (§7)
Errors: ProblemDetails + `code` (snake_case); payment refusals also carry `message` (§17 string) and `balanceOwed`. Wire names: `pending|completed|failed`, `insufficient_funds|account_unavailable|not_received`, `available|nothing_to_pay|above_limit|payment_pending`, `purchase|payment`.
- `GET /api/cardholder/cards/{id}` → `CardholderView` (pure read).
- `POST /api/cardholder/cards/{id}/quotes` (optional `X-CardLab-Origin: deploy-smoke`) → `201 Quote`; `409 nothing_to_pay|above_limit|payment_pending`.
- `POST /api/cardholder/cards/{id}/payments/{reference}/confirm` → `200 Receipt` (`repeated`); `409 balance_changed|quote_refused|payment_pending`; `404` unknown/other card's reference.
- `POST .../payments/{reference}/check` → `200 Receipt` (W-10 when unanswered); `GET .../payments/{reference}` → `200 Receipt`.
- `GET /api/fake-bank`; `PUT /api/fake-bank/steering` (`400` unknown value).
- Admin: `GET /api/cards/{id}` gains `balanceOwed`; `GET /api/cards/{id}/activity`; `GET /api/cards/{id}/payments`; `GET /api/cards/{id}/payments/{reference}` (`PaymentRecord`, AC-20).
- `POST /api/test/reset` extended (payment + bank tables, bank re-seeded).

## Flows and failures (§8, §9)
- Flow 1 complete; flow 2 unanswered → Pending, later `CheckStatusAsync` (bank call outside the lock, settle inside; `EffectiveAt = DebitedAtUtc ?? now`; `NotReceived` → Failed with W-4 text + "not received" badge); flow 3 balance changed → quote Refused, W-6, no bank call; flow 4 concurrent confirms (DEC-5); flow 5 crash between steps → stays Pending, next open settles; flow 6 fake bank in its own transaction with `sp_getapplock 'bank:{accountId}'` (5 s).
- Adapter exception → treated as `Unknown`; step 1 lock timeout → 503; step 3 lock timeout → one retry after 500 ms, then stays Pending with `Detail = "settle_lock_timeout"`.

## Observability (§12)
`PaymentStateChanged` (EventId 1003, one per state change incl. none → pending); `PaymentConfirmAttempt` (1004, `created|repeated|refused_balance_changed|refused_already_refused|refused_payment_pending`); `BankCallUnanswered` (1005, `debit|status`, Warning). Never more than the last four digits.

## Config (§11)
`CardLab:Payments:BankTimeoutSeconds` (default 10; integration tests 1 via `ApiFactory.WithBankTimeout`), `CardLab:Payments:SettleLockRetryMs` (500), `CardLab:Seed:LinkedAccount:{DisplayName, AccountNumber, BankAccountRef, OpeningBalance, AuthorityBasis}`, `CardLab:TestEndpoints`. No new secrets, repo variables or Bicep changes.

## UI rules (§4.1, checkable)
001 rules apply (contrast, keyboard, text badges, no modals, no icons, tokens only). Added: tokens `--color-status-pending` `#92400E` on `--color-status-pending-bg` `#FEF3C7`; confirm button pending state with `aria-disabled` + guard (001 BUG-010); review screen = W-1 lines + confirm button in one `<main>`, nothing else; outcome messages are server strings verbatim in `Notice` (`role="status"` pending/completed, `role="alert"` failed/refused); each failed/refused outcome offers its next-step control; fake-bank section heading contains "Fake bank", last in DOM, never on review/receipt; cardholder pages usable at 768 px.

## Testing (§15)
Boundaries as 001. New harness: `tests/CardLab.TestKit/Builders/PaymentBuilders.cs` (`QuoteBuilder`, `PaymentBuilder`, `LinkedAccountBuilder`, `BankOutcomes`); `tests/CardLab.Domain.Tests/Generators/PaymentArbitraries.cs`; `InMemoryCardStore` extended + `Contracts/PaymentStoreContractTests.cs`; `Fakes/FakeBank.cs` (`NextDebit`, `NextStatus`, `Hang`, `Gate`) + `Contracts/CurrentAccountBankContractTests.cs` (run vs `FakeBank` and `SqlFakeBank`); `FakePaymentReferences`; `ApiFactory.WithBankTimeout`; typed `ApiClient` additions; `PanScanner` second pattern; E2E page objects `CardholderPage`, `ReviewPage`, `ReceiptPage`, `PaymentRecordPage`; `api.ts` `setBankOutcome`, `setStatusCheck`, `simulatePurchase`, `cardholderView`; web `FakeApiClient` cardholder flow. Extension recipes: §15.4.

AC → test map (unit root `tests/CardLab.Domain.Tests`, integration `tests/CardLab.Integration.Tests`, E2E `tests/e2e/src`):
| AC | Test |
|---|---|
| AC-1 | T-U-1 `Payments/BalanceRulesTests.cs`; T-I-1 `Payments/CardholderViewEndpointTests.cs` |
| AC-2 | T-I-2 `Payments/ConfirmEndpointTests.cs` |
| AC-3 | T-U-2 `Payments/BalanceRulesTests.cs` |
| AC-4 | T-I-1; T-E-1 `payments/pay.spec.ts` |
| AC-5 | T-U-3 `Payments/WordingTests.cs`, T-U-4 `Payments/BalanceRulesTests.cs`; T-E-2 `payments/edge.spec.ts` |
| AC-6 | T-U-5 `Payments/WordingTests.cs`; T-E-1 |
| AC-7 | T-U-10 `Application/PaymentServiceTests.cs`; T-I-2; T-E-1 |
| AC-8 | T-U-11; T-I-3 `Payments/ConfirmRefusalTests.cs`; T-E-2 |
| AC-9 | T-U-12; T-I-4 `Payments/IdempotencyTests.cs` |
| AC-10 | T-U-13; T-I-5 `Payments/IdempotencyTests.cs` (20 parallel confirms) |
| AC-11 | T-U-6 `Payments/ConfirmRulesTests.cs`; T-U-14; T-E-3 `payments/pending.spec.ts` |
| AC-12 | T-U-15; T-E-2 |
| AC-13 | T-I-6 `Payments/TimeoutTests.cs`; T-E-3 (≤ 12 s) |
| AC-14 | T-U-16; T-I-7 `Payments/StatusCheckTests.cs`; T-E-3 |
| AC-15, AC-16 | T-U-17, T-U-18; T-I-7 |
| AC-17 | T-U-7 `Payments/PaymentStateMachineTests.cs`; T-P-4 |
| AC-18 | T-I-8 `Payments/BothSidesTests.cs` |
| AC-19 | T-I-9 `Payments/LimitsUnaffectedTests.cs` |
| AC-20 | T-I-10 `Payments/PaymentRecordEndpointTests.cs`; T-E-6 `payments/record.spec.ts` |
| AC-21, AC-37 | T-I-11 `Api/TestEndpointsTests.cs` |
| AC-22 | T-I-12 `Payments/FakeBankPersistenceTests.cs` |
| AC-23 | T-I-13 `Privacy/NoPanTests.cs`; T-E-4 `payments/privacy.spec.ts` |
| AC-24 | T-U-4; T-W-2 `pages/cardholder/CardholderView.test.tsx` |
| AC-25 | T-P-1..T-P-4 `Properties/PaymentProperties.cs` |
| AC-26 | T-E-7 `smoke/pay.spec.ts` (≤ 3 s on `demo`) |
| AC-27 | T-I-14 `Logging/PaymentEventTests.cs` |
| AC-28 | T-E-5 `payments/a11y.spec.ts` |
| AC-29 | T-C-1 (validator) |
| AC-30 | T-U-19; T-I-3 |
| AC-31 | T-I-15 `Payments/FakeBankContractTests.cs`; T-I-2 |
| AC-32 | T-I-6 |
| AC-33 | T-E-4 |
| AC-34 | T-I-7; T-E-3 |
| AC-35 | T-U-20; T-E-2 |
| AC-36 | T-U-21; T-I-7 |
| AC-38 | T-U-22 |
| (contract) | T-U-23 `Fakes/InMemoryPaymentStoreContractTests.cs`, T-U-24 `Fakes/FakeBankContractTests.cs`; T-I-16 `Store/SqlPaymentStoreContractTests.cs`, T-I-15 |
| (web) | T-W-1 `format/money.test.ts`; T-W-3 `pages/cardholder/Review.test.tsx`; T-W-4 `Receipt.test.tsx`; T-W-5 `FakeBankPanel.test.tsx`; T-W-6 `pages/PaymentRecord.test.tsx` |
| (perf) | T-I-17 `Performance/CardholderViewLatencyTests.cs` |
| (origin, DEC-15) | T-I-18 `Payments/OriginMarkerTests.cs`; T-E-7 |

Commands (`docs/sdlc/project.json`): unit `dotnet test tests/CardLab.Domain.Tests && npm test --prefix web -- --run` · integration `dotnet test tests/CardLab.Integration.Tests` · e2e `npm run e2e --prefix tests/e2e` (smoke: `-- --project smoke`).

## Budgets
- Performance (§13): confirm ≤ 3 s warm on `demo` when the bank completes (expected < 200 ms); bank budget 10 s (budget token alone decides "unanswered"; settlement runs with `CancellationToken.None`); review renders ≤ 2 s warm; cardholder view p95 ≤ 300 ms locally with 1,000 transactions + 200 payments; SPA bundle ≤ ~200 kB gzipped.
- Tests (§15.6): ~45 domain unit + 4 properties (~4 s), ~25 application unit (~2 s), ~15 web unit (~6 s), ~30 integration (~2 min), ~6 E2E specs (~4 min) + 1 smoke (~30 s). CI PR run ≤ 15 min.
- Cost: £0; no new resources.

## Delivery (§14)
No feature flag; migration and seed applied at startup; CI jobs unchanged, E2E and ZAP cover the new routes; deploy smoke `smoke/pay.spec.ts` (DEC-14); rollback = redeploy previous green commit (tables additive). `CL-3` in every PR title and body. Size committed L (estimate.md, Gate 2).
