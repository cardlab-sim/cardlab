---
feature: 002-card-current-transfers
title: Card and current account transfers: pay the card balance, draw cash from the card
lane: M
status: draft
approved_at:
---

# Card and current account transfers: Requirements

> CardLab is a fictional business credit card platform. Feature 001 gave the account
> administrator card controls (freeze, per-category limits) and an authorisation API. This feature
> gives each card a credit limit and a balance, links the business account to a current account
> held in an external system (behind a port, with a fake adapter in this repo), and lets the
> administrator move money between them: pay the card balance from the current account (full
> balance, minimum payment or a chosen amount) and draw cash from the card into the current account
> up to the available limit, with any fee shown before confirming. Money moves immediately, both
> accounts show the transaction, and the same transfer can never be sent twice by accident.

## 1. Context
- **Problem:** (proposed, Q-1) A business that runs its cards on CardLab has no way to settle what
  it owes on a card or to turn unused card credit into cash in its current account. Today the
  platform records purchases but has no notion of a balance, a credit limit, a payment or a fee, so
  nothing can be paid, drawn or reconciled; the administrator would have to do it outside the
  platform.
- **Trigger / why now:** (proposed, Q-1) First money movement on the platform. It introduces the
  concepts every later money feature needs (balance, available limit, fee, transfer, idempotency)
  and the first outbound port to an external system (the current account), with the compliance
  obligations that a customer-facing fee and a minimum payment carry (OB-1..OB-9).
- **Horizon:** (proposed, Q-3) **Long-lived practice product**, as feature 001: the balance,
  transfer and port models are built to last.
- **Constraints:**
  - Stack and delivery loop fixed by feature 001 (ASP.NET Core, React + TypeScript, SQL Server,
    Azure App Service F1, GitHub Actions; xUnit v3, FsCheck, Playwright; ADR-001..ADR-005).
  - Azure running cost stays near £0; fictional data only; no secrets in the repo.
  - Money movement is the high-risk path: strictest review tier (tier 3 covers `src/CardLab.Domain`,
    infra and migrations) and property-based tests.
  - Card numbers never appear in logs, responses or UI (NFR-1 of feature 001 still holds).
  - Public repository: documentation describes only CardLab's own product context.
  - Compliance register `docs/compliance/obligations.md` (R2 triggers: a fee shown to the
    customer, confirmation wording, minimum-payment or allocation logic, records of acceptance,
    first-of-kind money movement). Entries OB-1..OB-9 apply; a compliance pre-read runs after
    round 1 and its questions are folded into this doc.
  - Lane M (human's choice; L was recommended). Lane M holds as long as the scope excludes
    statement cycles and interest accrual (Q-6); either of those would reopen the lane.
  - Soft budget as in 001: each SDLC phase under about one working day of agent time.
- **Current system:** Domain has `Money` (non-negative GBP, 2 dp, subtraction floors at £0.00),
  cards (`CardSummary`/`CardDetail`/`CardState`, status active|frozen), per-category `SpendLimit`
  per period, append-only `TransactionRecord` (amount, category, merchant, timestamps, outcome,
  reason) and append-only audit events. Ports: `ICardStore`/`ICardWriter` (per-card lock,
  ADR-002), `IClock`, `ICardNumberLookup`, `ICurrentActor` (one seeded admin, no login). Services:
  `AuthorisationService`, `CardControlsService`. Minimal API, React admin UI (card list, card
  detail with limits, transactions, audit, simulate purchase). Test harness per ADR-005 (domain
  unit + FsCheck properties, port contract suites run on the in-memory and SQL adapters,
  integration tests on real SQL via WebApplicationFactory, Playwright E2E, web `fakeApiClient`).
  Missing today: credit limit, available limit, balance, minimum payment, interest, fee, current
  account, transfer, idempotency key, customer actor.

## 2. Goals & success metrics
| ID | Goal | Metric | Target | By when |
|----|------|--------|--------|---------|
| G-1 | (proposed, Q-1) The administrator can pay a card's balance from the linked current account (full, minimum, chosen amount) end to end in the deployed environment, and both accounts show the payment. | Playwright journey against the deployed URL | 100% of payment ACs validated | feature validation |
| G-2 | (proposed, Q-1) The administrator can draw cash from a card into the current account with the fee and total shown on one confirm screen before the money moves. | Playwright journey + compliance ACs traced to OB-1, OB-7, OB-8, OB-9 | 100% of cash-draw and disclosure ACs validated | feature validation |
| G-3 | (proposed, Q-1) No transfer is ever applied twice or half-applied: repeated confirmations, concurrent requests and external-system failures leave both accounts consistent. | Property-based and integration tests over idempotency, consistency and failure paths | all consistency ACs validated; property tests green in CI | feature validation |

## 3. Users & journeys
### Personas
| Persona | Job to be done | Context (device, frequency, skill) |
|---|---|---|
| Account administrator (Admin) (proposed, Q-4) | Settle what the business owes on each card and move spare card credit into the business's current account when cash is needed. | Desktop browser, a few times a month, non-technical finance/office manager; the seeded identity of feature 001, no login. |
| Current account system (external, system actor) | Hold the business's current account: accept debit and credit instructions from CardLab and report their outcome. | Machine-to-machine behind a port; a fake adapter stands in for it in this repo and in tests. |

### Journeys
(proposed shapes; settled in rounds 1-2)

**J-1 Pay the card balance** (Admin): 1. Open a card; see its balance, available limit and
minimum payment. 2. Choose *Pay*: full balance, minimum payment or a chosen amount. 3. See a
confirm screen stating the amount to move from the current account to the card (no fee). 4.
Confirm → money moves immediately; the card's balance and available limit update; the payment
appears in the card's history and in the current account's entries. *Edge/failure paths:* balance
£0.00 (nothing to pay); chosen amount above the full balance; current account has insufficient
funds; current account system unavailable; outcome unknown after the instruction was sent;
confirm pressed twice; a stale confirm after the balance changed.

**J-2 Draw cash from the card** (Admin): 1. Open a card; see the available limit. 2. Choose
*Draw cash*, enter an amount. 3. See a confirm screen stating the amount, the fee, the total that
will be added to the card balance and what the current account will receive. 4. Confirm → money
moves immediately; the card's balance rises by amount + fee, the available limit falls; the cash
draw and its fee appear in the card's history and the credit appears in the current account.
*Edge/failure paths:* amount + fee exceeds the available limit; card frozen; current account
system unavailable; outcome unknown; confirm pressed twice; quote expired.

**J-3 Purchases against the available limit** (Issuer processor, as in 001 J-3): a purchase that
would take the balance over the credit limit is declined (Q-7); approved purchases raise the
balance and lower the available limit.

## 4. Scope
**In scope:** (proposed, refined through the rounds)
- Per-card credit limit, balance and available limit (Q-5); balance effects of approved
  purchases, payments, cash draws and fees.
- Payment from the linked current account to a card: full balance, minimum payment, chosen amount.
- Cash draw from a card to the linked current account, with a fee shown before confirmation.
- A two-step quote/confirm flow where the quote fixes what the customer saw (amount, fee, total)
  and confirming the same quote twice moves money once.
- One linked current account per business account, held in an external system behind an
  outbound port; a fake adapter in this repo; contract tests shared by fake and future real adapters.
- Transfer records on the card side; the current account's entries readable through the port.
- Audit and structured log events for every transfer; records of what was shown and accepted.
- Admin UI: balance/available/minimum on the card detail page, pay and draw-cash flows, transfer
  history.

**Out of scope / non-goals:** (proposed)
- A real current account integration (only the port and a fake adapter).
- Cardholder self-service and login (the seeded admin acts, as in 001).
- Interest accrual, statements and due dates (Q-6); late fees; direct debits or scheduled
  payments; refunds and chargebacks; currency other than GBP.
- Changing a card's credit limit from the UI (seeded per card).

**Deferred (later, not now):** real login and a cardholder persona; statement cycles; interest;
credit limit management; scheduled/automatic repayments; alerts.

## 5. Functional requirements
| ID | Requirement (what, not how) | Priority (must/should/could) | Source (G/J) |
|----|-----------------------------|------------------------------|--------------|
| R-1 | (draft) Every card has a credit limit, a balance (what the business owes on the card) and an available limit = credit limit minus balance, floored at £0.00; approved purchases, cash draws and fees raise the balance, payments lower it. | must | G-1, G-2, J-1, J-2, J-3 |

## 6. Non-functional requirements
| ID | Category | Requirement (measurable) | Priority |
|----|----------|---------------------------|----------|
| NFR-1 | consistency | (draft) A transfer is applied to both accounts exactly once or to neither; no sequence of retries, repeated confirmations or concurrent requests can apply it twice or leave only one side changed. | must |

## 7. Acceptance criteria
| ID | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-1 | R-1 | (draft) a card with credit limit £1,000.00 and balance £250.00 | the card is read | available limit £750.00 is shown |

## 8. Data & integrations
- **Data captured/stored:** (proposed) per card: credit limit, balance; transfers: id, card,
  direction (payment to card | cash draw to current account), amount, fee, total, quote shown
  (amount, fee, total, minimum and full balance at quote time), idempotency key, status, outcome
  reason, actor, UTC times, external reference. Fictional data; transfers and quotes are
  append-only and retained for the life of the card (retention of acceptance records per OB-9:
  Q-13).
- **Integrations:** outbound port to the current account system (debit, credit, outcome query by
  reference, balance/entries read); fake adapter in this repo. Inbound: none new.
- **Analytics/observability needs:** one structured log event per transfer attempt and per
  outcome (Q-13); minimum-only payment events recorded for monitoring (OB-6).

## 9. Rollout
(proposed, Q-14) No feature flag; seeded credit limits for the three cards and one seeded linked
current account with an opening balance; idempotent seeding; forward-only schema; rollback =
redeploy previous green commit.

## 10. Risks & assumptions
| Type | Statement | Impact if wrong | Mitigation / validation |
|------|-----------|-----------------|-------------------------|
| risk | Cross-system money movement with an external system that can fail or time out after the instruction was sent. | Money moved on one side only, or twice. | Quote/confirm with a single idempotency key; outcome query by reference; property and integration tests over every failure path (Q-11, NFR-1). |
| risk | Minimum payment and fee rules are regulated (OB-3..OB-8); getting the formula or the disclosure wrong is a compliance finding, not just a bug. | Rework after the compliance pre-read. | Formula, wording and records fixed in this doc with ACs traced to OB entries; compliance questions folded in. |
| assumption | Lane M is enough if interest and statements stay out of scope. | Design and build overrun. | Re-evaluate the lane at Q-6. |
| assumption | One business account, one linked current account, per-card balances is a realistic enough model. | Model rework when accounts become shared. | Keep the current account an explicit entity linked to the business account (Q-5). |

## 11. Design hints (non-binding, from the human)
- The current account is an external system behind a port; this feature ships a fake adapter.
- "The same transfer can never be sent twice by accident": the human's framing points at an
  idempotency key per transfer attempt; the quote/confirm shape proposed here is one way to carry
  it (design decides).
- Per-card lock (ADR-002) already serialises writes on a card; transfers can use it.

## 12. Discovery
| ID | Question | Agent | Status | Report | Outcome (1 line) |
|----|----------|-------|--------|--------|------------------|

## 13. Decisions log
| # | Decision | Why | Delegated? |
|---|----------|-----|------------|
| 1 | Lane M (L recommended). | Human's choice; holds while statements and interest stay out of scope. | no (human) |

## 14. Open questions
| ID | Question | Blocked by | Owner |
|----|----------|------------|-------|
| Q-1 | Problem, why now and success: confirm or correct the proposed problem statement, trigger and goals G-1..G-3; what is the smallest outcome still worth shipping? | | human |
| Q-2 | Cash draw fee rule and payment charges: proposed fee = 3% of the amount, minimum £3.00, rounded up to the penny, added to the card balance; amount + fee must fit in the available limit; payments to the card are free. | | human |
| Q-3 | External system failure semantics: proposed outcomes completed / failed (insufficient funds, unavailable, rejected) / unknown-then-resolved by querying the reference; nothing stays half-applied or re-sent blindly. | | human |
| Q-4 | Who initiates transfers: the seeded account admin (as 001) or a new cardholder persona? | | human |
| Q-5 | Balance scope: per card (each card has its own credit limit, balance, available) or one card account per business account shared by all cards? | | human |
| Q-6 | Minimum payment and interest model: no interest, minimum computed on the live balance (fees this month + 1% of balance, floor £5.00, capped at balance) vs statement cycle vs interest accrual. | | human |
| Q-7 | Do purchases check the available limit (new decline reason) or does the available limit only cap cash draws? | | human |
| Q-8 | How "both accounts show the transaction" is made visible: a read-only linked current account panel in the admin UI, or card-side only with the current account verified through the fake in tests. | Q-4, Q-5 | human |
| Q-9 | Idempotency and quote semantics: quote validity window, what happens when the balance changed after the quote, repeated confirm returns the same result, same key with different payload rejected. | Q-3 | human |
| Q-10 | Amount bounds: minimum transfer amount; chosen payment amount range (may it be below the minimum payment? above the full balance never); cash draw minimum; rounding. | Q-2, Q-6 | human |
| Q-11 | Concurrency and card state: transfers under the per-card lock vs in-flight authorisations; payment to a frozen card allowed, cash draw from a frozen card refused? | Q-7 | human |
| Q-12 | Payment allocation (OB-3): with no interest, is allocation moot, or do we model purchase/cash buckets with notional rates and allocate highest-rate first? | Q-6, compliance pre-read | human |
| Q-13 | Records, audit, logs and retention: what the stored quote/acceptance record contains (OB-9), audit events per transfer, minimum-only payment event (OB-6), log fields, retention set by Compliance. | Q-9, compliance pre-read | human |
| Q-14 | Seed data and rollout: credit limits per seeded card, current account opening balance, fake adapter behaviours (insufficient funds, unavailable, timeout triggers), feature flag or not. | Q-5, Q-8 | human |
| Q-15 | NFRs: latency targets for quote and confirm (local and deployed), UI/accessibility baseline reuse from 001, performance with history size. | Q-8 | human |

## 15. Glossary
| Term | Meaning |
|------|---------|
| Card balance | What the business owes on a card: approved purchases + cash draws + fees - payments, never below £0.00. |
| Credit limit | The most a card's balance may reach. |
| Available limit | Credit limit minus balance, never below £0.00. |
| Linked current account | The business's current account, held by an external system and reached through a port. |
| Payment | A transfer from the current account to a card that reduces the card balance. |
| Cash draw | A transfer from a card to the current account that raises the card balance by the amount plus the fee. |
| Minimum payment | The smallest payment the business is expected to make on a card balance. |
| Quote | The amounts (amount, fee, total, balances) shown to the customer before confirming a transfer; confirming it moves the money once. |
| Idempotency key | A value that identifies one transfer attempt so that repeating it never moves money twice. |

## Change log
| Date | Change | Reason (BUG-/Q-/human) |
|------|--------|------------------------|
| 2026-11-07 | Draft created from the human's idea and the context scan; context frontier opened (Q-1, Q-3..Q-7 with Q-2 fee rule), product frontier queued (Q-8..Q-15). | round 1 |
