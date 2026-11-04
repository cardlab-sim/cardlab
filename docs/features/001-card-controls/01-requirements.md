---
feature: 001-card-controls
title: Card controls: freeze/unfreeze and per-category spend limits
lane: L
status: approved
approved_at: 2026-11-04T21:56Z
---

# Card controls: Requirements

> CardLab is a fictional business credit card platform. This first feature lets a business
> account administrator freeze and unfreeze a card and set per-category spend limits per period;
> a frozen card declines every transaction, and a transaction that would take a category over its
> limit declines. Every control change is recorded as an audit event. Because the repo is empty,
> this feature also establishes CardLab's delivery loop (CI with tests and security scans, deploy
> to Azure), which every later feature reuses.

## 1. Context
- **Problem:** A business that issues cards to its staff needs to stop a card instantly (lost
  card, leaver, suspicious activity) and to cap what a card can spend on a given category
  (travel, fuel, software, ...) within a period, so that spend stays inside policy without
  chasing people after the fact. CardLab has no such controls today: the repository contains
  only a README.
- **Trigger / why now:** First feature of the platform. It carries the delivery loop (build,
  tests, static and dynamic security scans, deploy) that every later feature will reuse.
- **Horizon:** **Long-lived practice product.** More features will build on this one, so the
  harness, data model and audit model are built to last, not as throwaway.
- **Constraints:**
  - Stack fixed by the README: C# / ASP.NET Core, React + TypeScript, SQL Server (LocalDB
    locally, Azure SQL free tier in the cloud), Azure, GitHub Actions; xUnit and Playwright.
  - Azure running cost must stay near £0.
  - Fictional data only; no secrets in the repo (GitHub Actions authenticates to Azure via OIDC).
  - Money and limit logic is the high-risk path: strictest review tier and property-based tests.
  - Card numbers must never appear in logs.
  - Public repository: documentation may describe only CardLab's own product context.
  - No hard deadline; soft budget: each SDLC phase stays under about one working day of agent
    time (so discovery spikes are affordable but should be rare).
- **Current system:** Empty repository (README only). No code, tests, CI, project settings or
  earlier features.

## 2. Goals & success metrics
| ID | Goal | Metric | Target | By when |
|----|------|--------|--------|---------|
| G-1 | An administrator can freeze a card and the next transaction on it declines, end to end in the deployed environment. | Playwright journey passing against the deployed URL | 100% of freeze/unfreeze ACs validated | feature validation |
| G-2 | Per-category limits per period are enforced correctly. | Property-based tests over limit arithmetic and period boundaries, plus ACs | all limit ACs validated; property tests green in CI | feature validation |
| G-3 | The delivery loop runs on every PR and deploys green builds to Azure. | CI workflow with build, xUnit, Playwright, CodeQL, ZAP; deploy workflow via OIDC | every PR shows the checks; a green merge to main deploys automatically | feature validation |

## 3. Users & journeys
### Personas
| Persona | Job to be done | Context (device, frequency, skill) |
|---|---|---|
| Account administrator (Admin) | Keep company card spend inside policy: freeze a card the moment something is wrong, set and adjust category limits. | Desktop browser, a few times a week, non-technical finance/office manager. In this feature a single seeded admin identity acts without logging in. |
| Issuer processor (system actor) | Ask CardLab, for each card transaction, whether to approve or decline. | Machine-to-machine via the authorisation API; simulated in this feature by tests and by a "simulate purchase" form in the admin UI. |

### Journeys
**J-1 Freeze and unfreeze a card** (Admin): 1. Open the card list. 2. Pick a card, choose
*Freeze*. 3. The card shows as frozen; an audit event appears. 4. A purchase is attempted on the
card → declined with reason `card_frozen`. 5. Admin chooses *Unfreeze* → next purchase is
approved. *Edge/failure paths:* freezing an already frozen card (no-op, no duplicate audit
event); unfreezing a card that is not frozen (no-op); unknown card.

**J-2 Set a category limit** (Admin): 1. Open a card. 2. Add a limit: category, period, amount.
3. The limit is listed; an audit event records it. 4. Later, change the amount or remove the
limit → audit events record old and new values. *Edge/failure paths:* invalid amount; a second
limit for the same card/category/period replaces the first rather than duplicating it; lowering
a limit below spend already made in the current period (allowed; further spend declines until the
period resets).

**J-3 Authorise a purchase** (Issuer processor, or Admin via the simulate form): 1. A transaction
request arrives (card number, amount, category, merchant, timestamp). 2. CardLab answers approve
or decline with a reason code. 3. The transaction and its outcome appear in the card's
transaction history. *Edge/failure paths:* card frozen → decline; category spend in the period
would exceed the limit → decline; card unknown → decline; malformed request → rejected without
creating a transaction; two requests at once for the same card and category must not both be
approved if together they exceed the limit.

## 4. Scope
**In scope:**
- Freeze / unfreeze a card; per-category spend limits per period; the authorisation decision
  (approve/decline with reason) for incoming transactions; transaction history per card.
- Audit events for every control change, readable per card.
- Admin web UI: card list, card detail (status, limits, transactions, audit), simulate purchase.
- Seed data: one fictional business account, one admin identity, a handful of fictional cards.
- Delivery loop: CI on every PR (build, xUnit, Playwright, CodeQL, ZAP), ticket-key check,
  deploy to Azure through GitHub Actions with OIDC, one cloud environment.

**Out of scope / non-goals:**
- Real authentication or authorisation (the admin is a seeded identity; no login screen).
- Cardholder self-service (a cardholder cannot see or freeze their own card).
- Card issuance, cancellation, replacement or PIN management.
- Integration with a real card network or issuer processor; CardLab only exposes the
  authorisation API that such a processor would call.
- Multi-currency; merchant-category-code (MCC) mapping (the request names the category).
- Notifications (email/SMS) on declines or control changes.
- An external ticket tracker (a tracker may be wired up by a later feature; see design hints).
- A global audit search across cards (audit is read per card).

**Deferred (later, not now):** real login; cardholder view; per-card total limit across all
categories; alerts on approaching a limit; MCC mapping tables; admin-defined categories;
idempotency keys / retries on the authorisation API (a repeated request is a second transaction); metrics backend and dashboards (telemetry export is a later feature).

## 5. Functional requirements
| ID | Requirement (what, not how) | Priority (must/should/could) | Source (G/J) |
|----|-----------------------------|------------------------------|--------------|
| R-1 | An administrator can freeze a card. Freezing an already frozen card changes nothing. | must | G-1, J-1 |
| R-2 | An administrator can unfreeze a frozen card. Unfreezing a card that is not frozen changes nothing. | must | G-1, J-1 |
| R-3 | A transaction on a frozen card is declined with the reason `card_frozen`. | must | G-1, J-3 |
| R-4 | An administrator can set, change and remove a spend limit for a card per spend category per period. A card has at most one limit per (category, period); setting one that exists replaces it. Categories: Travel, Fuel, Meals & Entertainment, Software & Subscriptions, Office Supplies, Advertising, Professional Services, Other. Periods: day, week (Monday to Sunday), month, all calendar periods in the business account's timezone (Europe/London). | must | G-2, J-2 |
| R-5 | A transaction is declined with the reason `limit_exceeded` if, for any limit on its card and category, the approved spend in the period containing the transaction plus the transaction amount would exceed the limit amount. Spend exactly equal to the limit is approved. Declined transactions never count towards spend. A category with no limit is unlimited. A limit of £0.00 declines every transaction in that category. | must | G-2, J-3 |
| R-6 | Every control change (freeze, unfreeze, limit set/changed/removed) is recorded as an immutable audit event with actor, UTC time, card (internal id and last four digits), action and old/new values; events are never edited or deleted and the administrator can read them per card, newest first. | must | G-1, G-2, J-1, J-2 |
| R-7 | CardLab exposes an authorisation API that accepts a transaction request (card number, amount, category, merchant, timestamp) and returns an outcome with one reason code from {`approved`, `card_frozen`, `limit_exceeded`, `card_not_found`, `invalid_request`}; for a known card the response also states, for each limit on the card in the transaction's category, the remaining amount after the decision. Every decision for a known card is stored in that card's transaction history with outcome and reason; requests for unknown cards or malformed requests store nothing. | must | G-1, G-2, J-3 |
| R-8 | The admin UI lets the administrator simulate a purchase on a card and see the outcome, and shows each card's transaction history with outcome and reason. | must | J-3 |
| R-9 | Every pull request runs build, unit/integration tests, end-to-end tests, static code scanning and a dynamic security scan, and fails if the ticket key is missing from the PR title and body. | must | G-3 |
| R-10 | A green build on the main branch deploys automatically to the Azure environment. | must | G-3 |
| R-11 | All amounts are GBP with at most two decimals. A transaction amount is greater than £0.00 and at most £1,000,000.00; a limit amount is between £0.00 and £1,000,000.00 inclusive. Values outside these rules are rejected with no side effects. | must | G-2, J-2, J-3 |
| R-12 | Concurrent authorisation requests for the same card are decided as if they had been processed one after another: the approved spend in a period never exceeds a limit. | must | G-2, J-3 |
| R-13 | The period of a transaction is derived from the timestamp supplied in the request. Limit changes apply immediately: lowering a limit below spend already made in the current period is allowed (further spend in that category declines until the period resets, existing transactions are untouched); removing a limit makes the category unlimited at once. A repeated request is a second transaction. | must | G-2, J-2, J-3 |
| R-14 | CardLab exposes a health endpoint that reports the running build's commit and whether the database is reachable. | must | G-3 |

## 6. Non-functional requirements
| ID | Category | Requirement (measurable) | Priority |
|----|----------|---------------------------|----------|
| NFR-1 | privacy/security | No full card number appears in any log line, error message, audit event, stored record other than what is needed to look a card up, API response or UI screen; at most the last four digits are shown. | must |
| NFR-2 | quality | Money and limit logic (R-5, R-11, R-12 and period arithmetic) is covered by property-based tests and reviewed at the strictest review tier. | must |
| NFR-3 | cost | Azure running cost for the deployed environment stays near £0 per month (free tiers only). | must |
| NFR-4 | security | No secrets in the repository; cloud deployment authenticates with OIDC. | must |
| NFR-5 | performance | An authorisation decision completes in at most 300 ms at the 95th percentile, measured by the integration test suite against the local database with 1,000 stored transactions on the card; the simulate-purchase round trip in the deployed environment completes in at most 2 s once warm; the deployed site answers a warm-up request within 60 s. No availability target (free tier). | must |
| NFR-6 | accessibility/i18n | UI text in English (UK); amounts shown as £1,234.56; dates and times shown in Europe/London; works on the latest desktop Chrome, Edge and Firefox; an automated accessibility check in the end-to-end suite reports zero critical or serious violations on every page; every action is keyboard-operable. | should |
| NFR-7 | observability | Every authorisation and every control change produces one structured log event (authorisation: internal card id, last four digits, category, amount, outcome, reason, duration; control change: actor, card, action) in a format compatible with OpenTelemetry export, so a later feature can ship them to a telemetry backend without changing the application. No metrics backend or dashboard in this feature. | must |

## 7. Acceptance criteria
| ID | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-1 | R-1, R-6 | an active card | the admin freezes it | the card's status is frozen and one audit event exists: action freeze, actor = the seeded admin, UTC time, card last four, old status active, new status frozen |
| AC-2 | R-1, R-6 | a frozen card | the admin freezes it again | the status stays frozen and no new audit event is created |
| AC-3 | R-2, R-6 | a frozen card | the admin unfreezes it | the status is active and one audit event exists with action unfreeze, old frozen, new active |
| AC-4 | R-3, R-7 | a frozen card with no limits | an authorisation request for £1.00 Fuel arrives | the response is declined with reason `card_frozen`, the transaction appears in the card's history as declined, and the card's spend is unchanged |
| AC-5 | R-2, R-3 | a card that was frozen and then unfrozen | an authorisation request within its limits arrives | the response is `approved` |
| AC-6 | R-4, R-6 | a card with no limits | the admin sets a limit Fuel / day / £50.00 | the limit is listed on the card and one audit event exists: action limit set, new value Fuel day £50.00 |
| AC-7 | R-4, R-6 | a card with Fuel / day / £50.00 | the admin sets Fuel / day / £80.00 | the card has exactly one Fuel/day limit, of £80.00, and an audit event exists: action limit changed, old £50.00, new £80.00 |
| AC-8 | R-4, R-6 | a card with Fuel / day / £80.00 | the admin removes it | the card has no Fuel/day limit and an audit event exists: action limit removed, old £80.00 |
| AC-9 | R-4, R-11 | any card | the admin sets a limit of -£1.00, £1,000,000.01 or £10.005 | the request is rejected, no limit is created or changed and no audit event is written |
| AC-10 | R-5, R-7 | a card with Fuel / day / £50.00 and £30.00 of approved Fuel spend on the same Europe/London day | an authorisation request for £20.00 Fuel arrives that day | the response is `approved` with remaining £0.00 for the Fuel/day limit |
| AC-11 | R-5, R-7 | the same card and spend as AC-10 | an authorisation request for £20.01 Fuel arrives that day | the response is declined with reason `limit_exceeded`, the transaction is stored as declined, and the Fuel/day remaining is still £20.00 |
| AC-12 | R-5 | the card after AC-11 | an authorisation request for £20.00 Fuel arrives that day | the response is `approved` (declined transactions do not count) |
| AC-13 | R-5 | a card with Fuel / day / £50.00 and £50.00 approved at 2026-10-24T22:30:00Z (23:30 British Summer Time) | a request for £10.00 Fuel arrives with timestamp 2026-10-24T23:30:00Z (00:30 on 25 October in Europe/London) | the response is `approved`, because the limit period is the Europe/London calendar day (under UTC it would still be 24 October and would decline) |
| AC-14 | R-5 | a card with Fuel / week / £100.00 and £100.00 approved on Sunday 2026-11-08 at 23:00 Europe/London | a request for £10.00 Fuel arrives on Monday 2026-11-09 at 00:30 Europe/London | the response is `approved` (weeks run Monday to Sunday) |
| AC-15 | R-5 | a card with Fuel / month / £500.00 and £500.00 approved on 2026-11-30 at 12:00 Europe/London | a request for £1.00 Fuel arrives on 2026-12-01 at 00:00 Europe/London | the response is `approved` |
| AC-16 | R-5 | a card with Fuel / day / £50.00 and Fuel / month / £200.00, £0.00 spent today and £180.00 approved this month | a request for £30.00 Fuel arrives | the response is declined with reason `limit_exceeded` (every limit on the category is evaluated) |
| AC-17 | R-5 | a card with Fuel / day / £0.00 | a request for £0.01 Fuel arrives | the response is declined with reason `limit_exceeded` |
| AC-18 | R-5 | a card whose only limit is Fuel / day / £50.00 | a request for £5,000.00 Travel arrives | the response is `approved` (no limit on Travel) |
| AC-19 | R-7 | a card number that does not belong to any card | an authorisation request for it arrives | the response is declined with reason `card_not_found` and no transaction is stored anywhere |
| AC-20 | R-7, R-11 | any card | a request arrives with a missing amount, an amount of £0.00, an unknown category or an unparseable timestamp | the response is declined with reason `invalid_request` and no transaction is stored |
| AC-21 | R-12 | a card with Fuel / day / £50.00 and no spend today | 10 requests for £10.00 Fuel arrive at the same instant | exactly 5 are `approved` and 5 are declined with `limit_exceeded`, and the card's approved Fuel spend today is £50.00 |
| AC-22 | R-8 | the card detail page | the admin submits the simulate-purchase form (amount, category, merchant) | the outcome and reason code are shown and the transaction appears at the top of the card's transaction history |
| AC-23 | R-6, R-8 | a card with the changes of AC-6..AC-8 | the admin opens the card's audit list | three events are shown newest first, each with actor, time, action and old/new values |
| AC-24 | NFR-1 | the journeys J-1..J-3 executed, including an unknown-card request | application logs, audit events, API responses, stored transaction and audit rows and UI screens are inspected | no full card number (16 consecutive digits) appears anywhere; only the last four digits do |
| AC-25 | NFR-2 | the limit and period arithmetic | the test suite runs in CI | property-based tests assert: approved spend per (card, category, period) never exceeds the limit; declines never change spend; remaining = limit - approved spend; period boundaries in Europe/London (including the two DST changes) assign each timestamp to exactly one day, week and month |
| AC-26 | R-9 | a pull request opened against the feature branch | CI runs | the checks build, unit/integration tests, end-to-end tests, CodeQL and ZAP are reported on the PR, and a PR whose title and body lack `CL-<n>` fails the ticket-key check |
| AC-27 | R-10 | a merge to main with green CI | the deploy workflow runs | the Azure environment serves the new build, identifiable by commit on its health endpoint |
| AC-28 | NFR-3 | the deployed Azure resources | their tiers are listed | every resource is on a free tier and the estimated monthly cost is at most £1 |
| AC-29 | NFR-4 | the repository and its workflows | they are inspected | secret scanning reports no secrets, and the deploy workflow holds no stored cloud credentials (OIDC only) |
| AC-30 | R-13, R-4 | a card with Fuel / day / £50.00 and £40.00 approved Fuel spend today | the admin lowers the limit to £30.00 and then a request for £0.01 Fuel arrives today | the limit is £30.00, the earlier £40.00 of transactions are unchanged, and the request is declined with `limit_exceeded` with remaining £0.00 |
| AC-31 | R-13, R-4 | the card of AC-30 | the admin removes the Fuel/day limit and a request for £1,000.00 Fuel arrives today | the response is `approved` |
| AC-32 | R-13 | a card with Fuel / day / £50.00 | two identical requests (same amount, merchant and timestamp) for £30.00 Fuel arrive one after the other | the first is `approved` and the second is declined with `limit_exceeded`; both appear in the transaction history |
| AC-33 | R-13 | a card with Fuel / day / £50.00 and £50.00 approved with timestamp 2026-11-04T10:00:00Z | a request for £10.00 Fuel arrives carrying timestamp 2026-11-05T10:00:00Z, regardless of the server's clock | the response is `approved` (the period comes from the request timestamp) |
| AC-34 | R-14 | the application is running | the health endpoint is called | it answers within 60 s with the build commit and a database reachability status |
| AC-35 | NFR-5 | a card with 1,000 stored transactions and limits in three categories, local database | the integration suite issues 200 authorisation requests | the 95th percentile decision time is at most 300 ms |
| AC-36 | NFR-5 | the deployed environment, already warmed up | the admin submits the simulate-purchase form | the outcome is shown within 2 s |
| AC-37 | NFR-6 | every page of the admin UI | the end-to-end suite runs its automated accessibility check and drives each action by keyboard only | zero critical or serious violations are reported and every journey completes |
| AC-38 | NFR-6 | the card detail page of a card with a £1,234.56 limit and a transaction at 2026-10-24T23:30:00Z | the page is rendered | the limit shows as £1,234.56 and the transaction time shows as 25 Oct 2026 00:30 (Europe/London) |
| AC-39 | NFR-7, NFR-1 | the journeys J-1..J-3 executed | the application log output is inspected | each authorisation and each control change has exactly one structured log event with the fields of NFR-7, and none contains a full card number |
| AC-40 | R-2, R-6 | an active (not frozen) card | the admin unfreezes it | the status stays active and no new audit event is created |

## 8. Data & integrations
- **Data captured/stored:** business account (name, timezone Europe/London); admin identity
  (display name); cards (internal id, cardholder name, last four digits, status, a lookup value
  derived from the fictional card number that cannot be reversed into it); spend limits (card,
  category, period, amount); transactions (card, amount, category, merchant, timestamp as
  supplied, outcome, reason); audit events (actor, UTC time, card, action, old/new values).
  All data is fictional; audit events and transactions are append-only and retained for the
  life of the card. No real personal data.
- **Integrations:** inbound authorisation API (called by an issuer processor; simulated here by
  tests, Playwright and the simulate-purchase form). Outbound: none.
- **Analytics/observability needs:** structured log events per authorisation and control change
  (NFR-7), a health endpoint (R-14). No metrics backend or dashboards in this feature; log
  events must be exportable by a later telemetry feature without application changes.

## 9. Rollout
- No feature flag: first feature, nothing to protect.
- Environments: local (LocalDB) and one Azure environment, deployed from main.
- Ticket: one GitHub Issue for this feature, created before the skeleton PR; its number gives
  the key `CL-<issue number>` used in every PR title and body.
- Human one-off setup before the deploy wave: Azure subscription/resource group, OIDC federated
  credential for GitHub Actions, repository variables (no secrets).
- Seed data (fictional): business account "Brightline Supplies Ltd" (timezone Europe/London);
  admin "Sam Ellis"; three active cards with no limits, named "Ops van", "Field sales" and
  "Marketing", each with a 16-digit fictional card number from a test range so the simulate form
  and end-to-end tests use known numbers. Seeding is idempotent: redeploys never duplicate data.
- Environment name: `demo`.
- Rollback expectation: re-run the deploy workflow on the previous green commit. Schema changes
  are forward-only (no database rollback); a bad migration is fixed forward.

## 10. Risks & assumptions
| Type | Statement | Impact if wrong | Mitigation / validation |
|------|-----------|-----------------|-------------------------|
| risk | Azure deployment needs one-off human setup (subscription, OIDC federated credential, variables) that agents cannot do. | Deploy stage blocked. | Explicit, documented human step before the deploy wave. |
| risk | Free-tier Azure SQL and hosting may pause or throttle, making the deployed E2E run flaky. | G-3 hard to demonstrate. | Design picks tiers with known behaviour; E2E runs with retries/warm-up. |
| assumption | A single business account with a handful of seeded cards is enough to exercise every journey. | Multi-tenant concerns surface later. | Data model keeps account as an explicit entity. |
| assumption | The transaction timestamp supplied by the caller is trusted for period assignment (no clock skew handling). | Backdated requests could land in a closed period. | Acceptable for a simulated processor (decision 17); revisit when a real one is integrated. |
| risk | Europe/London period arithmetic (DST) is a classic source of off-by-one-hour bugs. | Wrong period assignment near DST changes. | AC-13 and property tests over DST boundaries (AC-25). |

## 11. Design hints (non-binding, from the human)
- Stack and tooling are fixed by the README (ASP.NET Core, React + TypeScript, SQL Server, Azure, GitHub Actions, xUnit, Playwright).
- CI is expected to include build, xUnit, Playwright, CodeQL and a ZAP scan; deploys go through GitHub Actions to Azure with OIDC.
- Ticket key check: keep the key pattern pluggable (e.g. a configurable regex), because a later feature may wire up an external tracker whose keys look different; this feature does not integrate any tracker.
- Card number lookup: a keyed hash or similar one-way value is one way to find a card by number without storing the number (design decides).

## 12. Discovery
| ID | Question | Agent | Status | Report | Outcome (1 line) |
|----|----------|-------|--------|--------|------------------|

## 13. Decisions log
| # | Decision | Why | Delegated? |
|---|----------|-----|------------|
| 1 | Lane L. | First feature, new product area, new stack, carries the delivery loop. | no (human) |
| 2 | Q-1: the whole delivery loop (CI + Azure deploy) is in this feature's scope. | The loop must be exercised on real product code from day one; deploy is the last wave. | no (human) |
| 3 | Q-2: horizon is a long-lived practice product. | Later features build on this harness and data model. | no (human) |
| 4 | Q-3: single persona (account admin) with a seeded, simulated identity; no login in this feature. | Smallest scope that still records "who" on audit events; real auth is a later feature. | no (human) |
| 5 | Q-4: transactions enter through an authorisation API, plus a simulate-purchase form in the UI that calls it. | Gives tests, Playwright and ZAP a real surface; mirrors a real issuer processor. | no (human) |
| 6 | Q-5: goals G-1..G-3 as proposed. | They map onto the three things the feature must prove. | orchestrator under the human's standing instruction |
| 7 | Q-6: no hard deadline; each phase under about one working day of agent time. | Keeps design investment proportionate. | orchestrator under the human's standing instruction |
| 8 | Q-7: one GitHub Issue per feature, key `CL-<issue number>`, enforced by a CI check with a pluggable key pattern. | Real, checkable link with zero tooling; stays compatible with a later external tracker. | orchestrator under the human's standing instruction |
| 9 | Q-8: fixed list of 8 categories; the authorisation request names the category. | Smallest deterministic model; MCC mapping deferred. | orchestrator under the human's standing instruction |
| 10 | Q-9: calendar day / week (Mon-Sun) / month in Europe/London. | Correct behaviour for a UK business; DST becomes an explicit test case. | orchestrator under the human's standing instruction |
| 11 | Q-10: category limits only, at most one per (category, period), all evaluated; declines don't count; no-limit = unlimited; per-card total deferred. | Smallest correct model. | orchestrator under the human's standing instruction |
| 12 | Q-11: audit events = control changes only, immutable, per-card read; authorisation decisions are transaction records. | Keeps the audit promise crisp and separate from the transaction stream. | orchestrator under the human's standing instruction |
| 13 | Q-12: GBP only, two decimals; transaction £0.01..£1,000,000.00; limit £0.00..£1,000,000.00, £0.00 blocks the category. | Explicit bounds make the arithmetic property-testable. | orchestrator under the human's standing instruction |
| 14 | Q-13: fixed reason codes `approved`, `card_frozen`, `limit_exceeded`, `card_not_found`, `invalid_request`; approved responses state remaining per applied limit; UI shows history with outcome and reason. | Observable limit state for tests and processors. | orchestrator under the human's standing instruction |
| 15 | Q-15: authorisation requests identify the card by a 16-digit fictional card number; CardLab stores only a one-way lookup value and the last four. | Makes NFR-1 real and testable. | orchestrator under the human's standing instruction |
| 16 | Q-14: seed data (one account, one admin, three named cards with known fictional numbers), environment `demo`, rollback = redeploy previous green commit, forward-only schema, idempotent seeding. | Deterministic E2E journeys; cheapest safe rollback on a free tier. | orchestrator under the human's standing instruction |
| 17 | Q-16: period from the caller's timestamp; limit lowering/removal applies immediately; no idempotency keys. | Fully testable without a controllable clock; smallest rules. | orchestrator under the human's standing instruction |
| 18 | Q-17: NFR-5 targets (300 ms p95 local with 1,000 transactions; 2 s warm round trip deployed; 60 s warm-up; no availability target). | Pins the hot path without a production SLO. | orchestrator under the human's standing instruction |
| 19 | Q-18: NFR-6 UI baseline (English UK, £ formatting, Europe/London display, latest desktop browsers, automated accessibility check with zero critical/serious violations, keyboard-operable). | Measurable bar at near-zero cost inside the E2E run. | orchestrator under the human's standing instruction |
| 20 | Q-19: structured, OpenTelemetry-compatible log events per authorisation and control change plus a health endpoint; no metrics backend or dashboards (a later feature exports telemetry). | Demonstrates NFR-1 on real log output at no cost; keeps the door open for telemetry export. | orchestrator under the human's standing instruction |

## 14. Open questions
| ID | Question | Blocked by | Owner |
|----|----------|------------|-------|

## 15. Glossary
| Term | Meaning |
|------|---------|
| Business account | A company that holds cards on CardLab; one is seeded in this feature. |
| Admin | The business account administrator who operates card controls. |
| Card | A business credit card issued by CardLab to a member of staff of a business account. |
| Freeze | A reversible state in which a card declines all transactions. |
| Spend category | One of the 8 fixed classifications of a transaction by what was bought. |
| Spend limit | The maximum amount a card may spend in one category within one period. |
| Period | Calendar day, week (Monday to Sunday) or month in Europe/London, over which spend in a category is accumulated against a limit. |
| Remaining | Limit amount minus approved spend in the current period, never below £0.00. |
| Authorisation | The approve/decline decision CardLab gives for one transaction request. |
| Issuer processor | The (simulated) system that asks CardLab to authorise transactions. |
| Audit event | An immutable record of who changed which control, when, from what to what. |
| Ticket key | `CL-<n>`: the GitHub Issue number that every PR of a feature references. |
| Health endpoint | An unauthenticated endpoint reporting the running build's commit and database reachability. |

## Change log
| Date | Change | Reason (BUG-/Q-/human) |
|------|--------|------------------------|
| 2026-11-04 | Draft created from the human's idea and the context scan; context frontier opened (Q-1..Q-7), product frontier queued (Q-8..Q-14). | round 1 |
| 2026-11-04 | Q-1..Q-7 settled (decisions 2-8): horizon, goals, personas, journeys J-1..J-3, scope and non-goals, R-7..R-10, NFR-4, rollout basics. Frontier recomputed: Q-8..Q-19. | round 1 answers |
| 2026-11-04 | Q-8..Q-13, Q-15 settled (decisions 9-15): categories, periods, limit rules, audit, currency bounds, reason codes, card identification; R-4..R-7 made precise, R-11, R-12 added; AC-1..AC-29 written. Frontier: Q-14, Q-16..Q-19. | round 2 answers |
| 2026-11-04 | Q-14, Q-16..Q-19 settled (decisions 16-20): edge rules (R-13), health endpoint (R-14), NFR-5..NFR-7, AC-30..AC-40, rollout details, observability; self-review aligned R-7 (remaining stated on every decision). Open questions empty; ready for Gate 1. | round 3 answers |
