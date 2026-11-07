---
feature: 002-full-balance-payment
title: "CL-3 slice 1: pay the full card balance from the linked current account"
lane: M
status: approved
approved_at: 2026-11-07T14:55Z
risk_class: R2
ticket: CL-3
---

# Full balance payment (CL-3, slice 1): Requirements

> CardLab is a fictional business credit card platform. Today an administrator can freeze cards and
> cap category spend, and purchases accumulate on each card, but nobody can pay anything off. This
> slice lets a cardholder (a seeded persona, no login) pay the **full** balance of their card from
> the business's linked current account. The money normally moves within seconds of confirming (if
> the bank does not answer straight away the payment shows as pending until its outcome is known),
> both accounts show the movement, and a payment can never be sent twice by accident or applied to only one side. The
> current account is an external system behind a port with a fake adapter that can complete, fail
> or time out. It is slice 1 of CL-3; minimum/chosen-amount payments, cash draw with a fee, and
> transfer history/notifications are later slices and only leave forward constraints here.

## 1. Context
- **Problem:** Every approved purchase increases what a card owes, and the only way to see or
  reduce it is to read the transaction list and do the arithmetic by hand. There is no balance, no
  payment, no notion of the business's own bank account. A card product with no way to repay is
  incomplete, and every later money feature (minimum payments, cash draw, history) needs the same
  foundations: a balance, a payment record with a lifecycle, and a port to the outside bank.
- **Trigger / why now:** CL-3 ("payments and transfers") was sized XL and split into four slices.
  Slice 1 is the first movement of money in the product and carries the shared foundations.
- **Horizon:** **Long-lived practice product** (inherited from 001). The balance, payment and
  port models are built to be extended by slices 2-4, not thrown away.
- **Constraints:**
  - Stack and delivery loop fixed by 001: .NET 10 Minimal APIs, EF Core on SQL Server, React 19 +
    TypeScript SPA, one App Service F1 host, Azure SQL free offer, GitHub Actions with OIDC. No new
    hosted services (run cost stays near £0).
  - All money code lives in `src/CardLab.Domain` (pure, high-risk path, review tier 3, property
    tests). Money is GBP, two decimals, `£0.01..£1,000,000.00` per transaction.
  - Card numbers never appear in logs, responses, records or screens beyond the last four.
  - Public repository: documentation describes only CardLab's own product context; fictional data.
  - Compliance scope (CD-1): every CardLab business customer is treated as a micro-enterprise on a
    regulated credit agreement, the strictest reading, so one behaviour serves all; OB-1, OB-3, OB-7,
    OB-8, OB-9 apply now (OB-3 is met trivially: one balance at one rate, no interest) and
    OB-4..OB-6 from slice 2. Each cardholder acts under a standing
    authority from the business to repay their own card from the linked current account; CardLab
    initiates the debit and the external bank is the account provider.
  - Compliance: first-of-kind money movement and customer-facing confirmation wording trigger an
    R2 review (`docs/compliance/obligations.md`). Relevant entries: OB-1 (charges and execution
    time before confirm), OB-3 (repayment allocation; trivially met with one balance), OB-7/OB-8 (fee and total on one screen before the action, nothing
    revealed after), OB-9 (record of what was shown and accepted, stored with the transfer), and
    OB-3/OB-5 as forward constraints for slice 2.
  - PR titles and bodies carry `CL-3` (the CI `ticket-key` check).
  - Soft budget as in 001: each SDLC phase under about one working day of agent time.
- **Current system:** 001-card-controls (approved, built). Cards, limits, authorisation decisions
  stored as append-only `Transaction` rows (spend = sum of approved rows), append-only
  `AuditEvent`, per-card atomic writes (`ICardStore.WithCardLockAsync`), seeded admin actor, admin
  SPA (`/`, `/cards/:id`), test-only `POST /api/test/reset`. No cardholder actor, no balance, no
  current account, no payment concept. Idempotency was an explicit non-goal of 001 for the
  authorisation API (a repeated authorisation is a second transaction, AC-32 there); that stays
  true for purchases and is **not** what this slice changes.

## 2. Goals & success metrics
| ID | Goal | Metric | Target | By when |
|----|------|--------|--------|---------|
| G-1 | A cardholder can pay the full card balance end to end in the deployed environment: balance shown, confirmed, money moved, both accounts updated. | Playwright journey against `demo` | 100% of payment ACs validated | feature validation |
| G-2 | A payment is never duplicated or half-applied, whatever the cardholder or the bank adapter does (double submit, repeat, failure, timeout, concurrent purchase). | Integration and property tests over the payment lifecycle | all safety ACs validated; property tests green in CI | feature validation |
| G-3 | The confirmation and its record satisfy the register entries that apply (OB-1, OB-3, OB-7, OB-8, OB-9) so the compliance partner can sign Gate 1 and later slices inherit the pattern. | Compliance pre-read decisions all decided; ACs for each applicable entry | zero open compliance decisions at Gate 1 | Gate 1 |

## 3. Users & journeys
### Personas
| Persona | Job to be done | Context (device, frequency, skill) |
|---|---|---|
| Cardholder | Clear what their card owes from the business's current account, quickly and with certainty that it happened exactly once. | Desktop browser, a few times a month, non-technical. In this slice a seeded persona (the cardholder named on a seeded card) acts without logging in. |
| Account administrator (Admin, from 001) | See that a card's balance was paid and that the payment appears in the card's history. | Existing admin SPA; no new admin actions in this slice. |
| Linked current account (external system) | Accept or refuse a debit request from CardLab and report the outcome; may fail to answer in time. | Behind a port; a fake adapter in this slice. |

### Journeys
**J-1 Pay the full balance** (Cardholder): 1. Open the cardholder view of their card. 2. See the
balance owed, the linked current account (name, masked number) and its available balance. 3.
Choose *Pay full balance*. 4. Review one confirmation screen: amount, fee (£0.00, no fee), when the money
moves (normally within seconds; pending if the bank does not answer), source and destination
accounts. 5. Confirm. 6. See a receipt: reference,
amount, time, outcome; the card balance is now £0.00 and the current account shows the debit.
*Edge/failure paths:* balance is £0.00 (nothing to pay, action unavailable); balance changed
between review and confirm (payment refused, review again); the bank refuses (insufficient funds
or other refusal: nothing moves, clear message); the bank does not answer in time (payment shown
as pending, no second payment allowed until it is resolved); the cardholder double-clicks or
refreshes and resubmits (exactly one payment).

**J-2 See the payment on both sides** (Cardholder, Admin): 1. After J-1, the cardholder view
lists the payment in the card's recent activity and in the current account's recent activity. 2.
The admin opens `/cards/:id`; the payment appears in the card's history as a payment, not a
purchase, and the card's balance is shown. *Edge/failure paths:* a failed payment appears as
failed on the card side only (the current account shows nothing, no money moved); a pending
payment appears as pending on the card side and is not counted as paid.

**J-3 Resolve a payment the bank did not answer** (Cardholder): 1. A payment is pending after a
timeout. 2. The cardholder view shows it as pending with *Check status*; opening the cardholder view or
the admin card detail also checks automatically. 3. Checking asks the bank for the outcome by
reference; the payment becomes completed (both accounts show it) or failed
(nothing moved) and the card is free to pay again. *Edge/failure paths:* the status check itself
times out (stays pending, can be checked again); a purchase approved while a payment is pending
(allowed; it increases the balance, which the pending payment does not cover).

## 4. Scope
**In scope:**
- A per-card **balance owed**: approved purchases minus completed payments, never negative, shown
  to the cardholder and the admin.
- One **linked current account** per business account (seeded, fictional), with a name, a masked
  account identifier and an available balance, shown to the cardholder.
- **Full-balance payment** by the cardholder: review (amount, fee, execution time, accounts),
  confirm, receipt; outcomes completed / failed / pending; resolving a pending payment.
- **Safety:** exactly-once submission (a repeated confirm is recognised and does not create a
  second payment), one payment in flight per card, both sides consistent at every moment anyone
  can observe them.
- **Current account port** with a fake adapter that can complete, refuse or time out, steerable
  for tests and demos; its ledger is visible and survives restarts.
- The payment appears in the card's transaction history (admin view) marked as a payment and
  never counts towards any spend limit.
- Compliance records: what the cardholder saw (amount, fee, execution time) and when they
  accepted it, stored with the payment.
- Cardholder web view (seeded persona, no login) for each seeded card.

**Out of scope / non-goals:**
- Interest, statements, statement dates, minimum-payment calculation.
- Purchases checking an available credit limit (authorisation rules are unchanged).
- Recurring payments, standing orders, payments to third parties, payments from any account
  other than the business's linked current account.
- Multi-currency; fees of any kind on this payment (the fee is £0.00 and is shown as such).
- Real authentication; a real bank integration (the port has only the fake adapter).
- Admin-initiated payments; cancelling or reversing a completed payment.
- Changing how purchases are authorised or recorded (001 behaviour, including "a repeated
  authorisation is a second transaction", is untouched).

**Deferred (later slices of CL-3, with the forward constraints this slice must respect):**
- **Slice 2, minimum and chosen-amount payments:** the payment model must carry an amount that
  is not necessarily the full balance and a payment kind (full / minimum / chosen); allocation of a
  part payment to the highest-rate debt first (OB-3, met trivially in this slice) gets real
  allocation acceptance criteria then, as does "any amount between minimum and full" (OB-5). This slice must not hard-code "amount = balance" into
  records or the port contract, only into the one option offered.
- **Slice 3, cash draw from the card with a fee shown before confirming:** the confirmation
  screen pattern (one screen, fee and total before the action, nothing revealed after: OB-7,
  OB-8) and the stored quote/acceptance record (OB-9) must be reusable with a non-zero fee. Slice 3 adds a cash balance that may carry a different rate: the payment record must then be able to state what the settled amount was applied to by debt type (OB-3); this slice records the settled amount and the balance owed immediately before and after it is applied so that extension is additive.
- **Slice 4, transfer history and notifications:** a cross-account transfer history with filters
  and notifications on outcome; this slice keeps a complete, queryable payment record (reference,
  kind, amount, accounts, timestamps, outcome, failure reason) so slice 4 only adds views.

## 5. Functional requirements
| ID | Requirement (what, not how) | Priority (must/should/could) | Source (G/J) |
|----|-----------------------------|------------------------------|--------------|
| R-1 | Each card has a **balance owed** equal to the sum of its approved purchases minus the sum of its completed payments, never below £0.00; declined purchases and failed or pending payments do not change it. The cardholder sees it on the cardholder view and the admin sees it on the card detail. | must | G-1, J-1, J-2 |
| R-2 | The business account has exactly one **linked current account** (seeded, fictional) with a display name, an account identifier shown masked (last four digits only), an available balance, and the standing authority granted by the business for each cardholder to repay their own card from it. The cardholder view shows the account; every payment record names the authority basis. | must | G-1, J-1 |
| R-3 | A cardholder can start a **full-balance payment** from their card's cardholder view when the balance owed is at least £0.01 and no payment on that card is pending. When the balance is £0.00 the action is unavailable and the view says there is nothing to pay. | must | G-1, J-1 |
| R-4 | Before confirming, the cardholder sees **one review screen** stating: the amount (the balance owed at review time), the fee (£0.00, stated as a figure with words; no fee from CardLab or the bank), the total, when the money moves (an execution-time statement that is also true in the pending path: normally within seconds of confirming; if the bank does not answer straight away the payment shows as pending until its outcome is known), the source (linked current account, masked) and the destination (the card, last four). No further charge or condition is revealed after confirmation. The strings are those of §17 (wording v1, approved by the compliance partner under CD-2); the ACs quote them. | must | G-1, G-3, J-1 |
| R-5 | Confirming pays **exactly the reviewed amount**. If the balance owed at confirmation differs from the reviewed amount (a purchase or another payment landed in between), the payment is refused with a clear message and the cardholder is shown the current balance to review again; nothing moves. The refused attempt is recorded against the reference (what was shown, when confirm was pressed, why it was refused). | must | G-2, J-1 |
| R-6 | A payment has a **lifecycle** with exactly one state at a time: `pending` (sent to the bank, outcome not yet known), `completed` (the bank debited the current account and the card balance is reduced) or `failed` (the bank refused or the payment was never sent; nothing moved). Every state change is recorded with its UTC time; a completed or failed payment never changes state again. | must | G-2, J-1, J-3 |
| R-7 | **Exactly once:** a payment confirmation is identified by a reference created when the review screen is shown. Confirming the same reference again (double click, refresh and resubmit, network retry) returns the outcome of the first confirmation and never creates a second payment or a second debit; each repeated confirm is recorded against the reference with its time. A reference that was refused (R-5) or that belongs to another card cannot start a payment; a new review mints a new reference. | must | G-2, J-1 |
| R-8 | **One in flight per card:** while a card has a pending payment, a new payment on that card is refused with a message that names the pending payment and offers to check its status. | must | G-2, J-3 |
| R-9 | **Both sides agree:** at every moment anyone can observe them, the card balance and the current account's ledger tell the same story: a completed payment is reflected on both, a failed payment on neither, and a pending payment is visible as pending on the card side and not counted as paid. No observer can ever see a payment reflected on one side only. | must | G-2, J-2 |
| R-10 | **Bank refusal:** when the current account refuses the debit (insufficient funds, account unavailable, or another refusal), the payment is `failed` with the refusal reason, nothing moves, the balance owed is unchanged and the cardholder sees the reason in the words of §17 and can try again later. | must | G-2, J-1 |
| R-11 | **Bank timeout:** when the current account does not answer within the time budget (NFR-2), the payment stays `pending`; the cardholder is told the outcome is not yet known and that no money will move twice. The cardholder can **check status**, and a status check also runs automatically whenever the cardholder view or the admin card detail of that card is opened; a check asks the bank for the outcome by reference and settles the payment as completed or failed; an unanswered check leaves it pending. The effective time of a settled payment is the bank's debit time when the bank reports one, otherwise the settle time. A pending payment is never retried as a new debit. | must | G-2, J-3 |
| R-12 | **Receipt:** after confirmation the cardholder sees a receipt with the payment reference, amount, fee, time (Europe/London), source and destination, and outcome (completed / failed with reason / pending) in the words of §17. | must | G-1, G-3, J-1 |
| R-13 | **Visible on both accounts:** a payment appears in the card's transaction history (admin card detail, and the cardholder view's recent activity: the last 10 purchases and payments, newest first) as a payment with its state, and a completed payment appears in the current account's recent activity (last 10 entries, newest first, on the cardholder view) as a debit with the same reference and amount. Payments never count towards any category spend limit and never change authorisation outcomes. | must | G-1, J-2 |
| R-14 | **Compliance record:** each payment, in every state (pending, completed, failed) and every refused reference, stores what the cardholder was shown on the review screen (amount, fee, total, execution-time statement, accounts; both as structured fields and as the rendered wording with a wording version), when they accepted it, who acted, every confirm attempt on the reference (time; created / repeated / refused), each bank outcome with its time, the balance owed immediately before and immediately after the settled amount is applied, the authority basis (standing authority of the business for that cardholder), and whether the bank outcome came from the fake bank's steering control (marked as a simulated outcome). The shown-and-accepted part is never changed; state changes are appended. The admin can see this record per payment. Retention is 6 years from the payment's final state, or for a refused reference 6 years from its refusal; a pending payment has no final state and is kept until it settles (CD-5, CD-8); not tied to the card's life; no product path deletes these records (the test reset exists only where `CardLab:TestEndpoints` is on, never on `demo`). (OB-9) | must | G-3, J-2 |
| R-15 | **Fake current account:** the current account behind the port is a fake. A clearly labelled *Fake bank: next outcome* control in its own section of the cardholder view (never on the review screen or the receipt), available in every environment including `demo`, sets the outcome of the next debit request to one of: complete; refuse (insufficient funds); refuse (account unavailable); time out with the debit applied; time out with the debit not received. The setting applies to the next debit request only, then reverts to complete; it changes nothing else (no ledger, balance or record edits). Outcomes produced under a steering setting are marked as simulated in the payment record. Independently of the control, a debit larger than the available balance is refused with insufficient funds. The fake's ledger (available balance, entries with reference, amount, time) persists across restarts and is reset to the seed by the test reset. | must | G-2 |
| R-16 | **Cardholder persona:** every seeded card has a cardholder view reachable without login; actions from it are recorded as performed by that card's cardholder (name), distinct from the seeded admin. | must | G-1, J-1 |
| R-17 | **Amount bounds:** a payment amount obeys the existing transaction bounds (£0.01..£1,000,000.00). If the balance owed exceeds £1,000,000.00 the full-balance option is unavailable with a message to contact the business's administrator (practically unreachable; keeps money arithmetic inside tested bounds). | should | G-2 |

## 6. Non-functional requirements
| ID | Category | Requirement (measurable) | Priority |
|----|----------|---------------------------|----------|
| NFR-1 | quality | Balance arithmetic, the payment state machine, exactly-once and one-in-flight rules are pure domain code under `src/CardLab.Domain`, covered by property-based tests and reviewed at tier 3. | must |
| NFR-2 | performance | The confirm round trip (confirm pressed to receipt shown) completes in at most 3 s when the bank completes, measured warm on `demo`; the time budget for the bank to answer is 10 s, after which the payment is reported pending; a review screen renders within 2 s warm on `demo`. | must |
| NFR-3 | privacy/security | No full card number or full current account number appears in any log, record, response or screen; at most the last four digits of each. No secrets in the repo; the fake adapter needs none. | must |
| NFR-4 | observability | Every payment state change produces one structured, OpenTelemetry-compatible log event (payment id, card id, last four, kind, amount, from-state, to-state, reason, duration) consistent with 001's events; no metrics backend. | must |
| NFR-5 | accessibility/i18n | The cardholder view follows 001's UI rules: English (UK), `£1,234.56`, Europe/London times, keyboard-operable, zero critical or serious automated accessibility violations, no modals, status as text never colour alone. | should |
| NFR-6 | cost | No new Azure resources; run cost stays near £0. | must |
| NFR-7 | compliance | The review screen wording and the stored quote/acceptance record satisfy OB-1, OB-3, OB-7, OB-8 and OB-9 as decided by the compliance partner in `compliance-preread.md`; the decisions are closed before Gate 1. | must |

## 7. Acceptance criteria
| ID | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-1 | R-1 | a card with approved purchases £120.00 and £30.50, a declined purchase £500.00 and no payments | the cardholder view and the admin card detail are opened | both show a balance owed of £150.50 |
| AC-2 | R-1, R-13 | the card of AC-1 after a completed payment of £150.50 | the balance is read | it is £0.00; the card's history lists the payment as a payment (not a purchase) with state completed |
| AC-3 | R-1 | a card with approved purchases £100.00, a failed payment of £100.00 and a pending payment of £100.00 | the balance is read | it is £100.00 (failed and pending payments do not count) |
| AC-4 | R-2, R-16 | the seeded data | the cardholder view of any seeded card is opened without login | it shows the card's name and last four, the cardholder's name as the acting person, the linked current account's name, its identifier masked to the last four, and its available balance |
| AC-5 | R-3 | a card with balance owed £0.00 | the cardholder view is opened | the pay action is unavailable and the view shows W-7 of §17: "Nothing to pay. Your card balance is £0.00." |
| AC-6 | R-3, R-4 | the seeded Ops van card (ending 0001) with balance owed £150.50 | the cardholder chooses *Pay full balance* | one review screen shows, on the same screen as the confirm action and not behind a link or later step, exactly the lines W-1 of §17 with amount £150.50, fee "£0.00 (no fee)", total £150.50, the execution-time statement, the line "From: Brightline Supplies Ltd current account, ending 7731", the line "To: Ops van card, ending 0001", and the button "Confirm payment of £150.50" |
| AC-7 | R-5, R-7, R-12 | the review screen of AC-6 and the bank set to complete | the cardholder confirms | the receipt shows a reference, £150.50, fee £0.00, the time in Europe/London, both accounts and the completed message W-2 of §17 with £150.50 and £0.00; the card balance is £0.00; the current account's available balance has decreased by £150.50 and its recent activity lists a debit with the same reference |
| AC-8 | R-5 | the review screen of AC-6, then a purchase of £20.00 is approved on the card before confirming | the cardholder confirms | the payment is refused with the message W-6 of §17 (new balance £170.50), nothing moves on either account, no pending or completed payment exists, the refused attempt is recorded against the reference with what was shown and why it was refused, and the current balance £170.50 is shown for a new review |
| AC-9 | R-7 | a confirmation that completed | the same reference is confirmed again (second click, refresh and resubmit, or an API retry) | the response is the original receipt; exactly one payment and exactly one current-account debit exist; the record lists each repeated confirm with its time |
| AC-10 | R-7 | two confirmations with the same reference sent at the same instant | both are processed | exactly one payment exists; both callers receive the same outcome |
| AC-11 | R-8, R-11 | a card with a pending payment | the cardholder opens the view and tries to pay | the pay action is refused with the message W-9 of §17 naming the pending payment's amount and reference and offering *Check status* |
| AC-12 | R-10 | the review screen of AC-6 and the bank set to refuse with insufficient funds | the cardholder confirms | the payment is failed with reason insufficient funds, the balance owed is still £150.50, the current account's available balance and activity are unchanged, and the receipt shows fee £0.00 and W-3 of §17 with £150.50 |
| AC-13 | R-11, NFR-2 | the review screen of AC-6 and the bank set to time out | the cardholder confirms | within 10 s plus 2 s the receipt shows outcome pending, fee £0.00, with the message W-5 of §17 (balance £150.50); the balance owed is still £150.50; the card view shows the pending payment with W-5 of §17 and *Check status* |
| AC-14 | R-11, R-9 | the pending payment of AC-13 and the bank now reporting it as completed by reference | the cardholder checks status | the payment is completed, the balance is £0.00, the current account shows the debit once, and the card may pay again |
| AC-15 | R-11, R-9 | the pending payment of AC-13 and the bank reporting it as not received | the cardholder checks status | the payment is failed, nothing moved, the balance is £150.50, and the card may pay again |
| AC-16 | R-11 | the pending payment of AC-13 and the bank timing out on the status check | the cardholder checks status | the payment stays pending with the message W-10 of §17 and may be checked again; no new debit was requested |
| AC-17 | R-6 | any payment | its state history is read | it has exactly one of pending / completed / failed at each recorded time, each change carries a UTC time, and no completed or failed payment has a later change |
| AC-18 | R-9, R-6 | the journeys J-1 and J-3 executed with complete, refuse and timeout outcomes | after each step both accounts are read | there is never a reading in which a payment is reflected on one account and not the other, except as pending on the card side only; no fee or charge entry appears on the receipt, either account's activity or the record other than the £0.00 shown on the review screen |
| AC-19 | R-13 | a card with limit Fuel / day / £50.00, £40.00 Fuel approved today and a completed payment of £40.00 | a purchase of £20.00 Fuel arrives today | it is declined `limit_exceeded` (payments do not reduce category spend) |
| AC-20 | R-14 | a payment in each state (completed, failed, pending) and a refused reference | the admin opens each record | it shows the amount, fee, total, execution-time statement and accounts exactly as displayed on the review screen (fields and rendered wording with its version), the acceptance time, the acting cardholder and the authority basis, the reference, every confirm attempt with its time, each bank outcome with its time, the balance owed immediately before and immediately after the settled amount was applied where settled, and whether the outcome was simulated; after a status check, a repeated confirm or a restart the shown-and-accepted part is unchanged |
| AC-21 | R-15 | the running application | the test reset is called | the current account's available balance returns to its seeded value and its activity is empty; card balances are £0.00 |
| AC-37 | R-15, R-14 | `CardLab:TestEndpoints` off (as on `demo`) and existing payment records | the test reset is called | it is unavailable (404, as 001 DEC-16) and payment and acceptance records are unchanged |
| AC-22 | R-15 | the fake current account | the application is restarted after a completed payment | the current account's activity still lists the debit and the available balance is still reduced |
| AC-23 | R-16, NFR-3 | the journeys executed from the cardholder view | audit, payment records, logs and responses are inspected | actions from the cardholder view name the card's cardholder, never the admin; no full card number or full account number appears anywhere |
| AC-24 | R-17 | a card whose balance owed is £1,000,000.01 | the cardholder view is opened | the pay action is unavailable with the message W-8 of §17 |
| AC-25 | NFR-1 | the balance and payment rules | the test suite runs in CI | property tests assert: balance = approved purchases − completed payments floored at £0.00 for any sequence; a sequence of confirmations with repeated references yields at most one payment per reference; at most one pending payment per card at any time; a completed or failed payment never changes state |
| AC-26 | NFR-2 | the deployed environment, warm, bank set to complete | the cardholder confirms a payment | the receipt is shown within 3 s |
| AC-27 | NFR-4, NFR-3 | the journeys J-1 and J-3 executed | the application log is inspected | each payment state change has exactly one structured event with the fields of NFR-4 and none contains a full card or account number |
| AC-28 | NFR-5 | the cardholder view, review screen and receipt | the end-to-end suite runs its accessibility check and drives the journey by keyboard only | zero critical or serious violations and the journey completes |
| AC-30 | R-7, R-5 | a reference refused because the balance changed (AC-8) | the same reference is confirmed again | it is refused again with the same message; no payment exists for it; a new review yields a different reference |
| AC-31 | R-15, R-10 | the fake bank set to complete, available balance £25,000.00 and a card owing £25,000.01 | the cardholder confirms | the payment fails with insufficient funds and the available balance is unchanged |
| AC-32 | R-15 | the fake bank set to time out | one payment is confirmed (pending) and, after it is resolved, a second payment is confirmed | the second completes: the setting applied to one request only |
| AC-34 | R-11 | a pending payment whose debit the bank has since applied | the cardholder view (or the admin card detail) is opened, without pressing *Check status* | the payment is completed, the balance is £0.00, and the payment's effective time is the bank's reported debit time |
| AC-38 | R-11 | a pending payment that the bank reports completed without a debit time | the status is checked | the payment is completed and its effective time is the settle time |
| AC-36 | R-1, R-11, R-14 | the pending payment of £150.50 on the Ops van card (AC-13), then a purchase of £20.00 approved on that card | the bank reports the payment completed and the status is checked | the balance owed is £20.00, and the payment record shows a settled amount of £150.50 with the balance owed £170.50 immediately before and £20.00 immediately after settlement |
| AC-35 | R-10 | the review screen of AC-6 and the fake bank set to refuse with account unavailable | the cardholder confirms | the payment is failed with reason account unavailable, nothing moved, and the receipt shows fee £0.00 and W-4 of §17 with £150.50 |
| AC-33 | R-15 | the cardholder view with the fake bank control set to refuse | the review screen and then the receipt are shown | neither shows the fake bank control; the failed payment's record is marked as a simulated outcome |
| AC-29 | NFR-7, R-4 | the compliance pre-read for this feature | Gate 1 is requested | every compliance decision is marked decided and the wording ACs (AC-5..AC-8, AC-11..AC-13, AC-16, AC-24, AC-35) quote §17 wording v1 as approved by the compliance partner (CD-2, CD-3) |

## 8. Data & integrations
- **Data captured/stored:** linked current account (one per business account: display name,
  masked identifier, available balance, fictional); payments (reference, card, kind = full in this
  slice, amount, fee, state, state history with UTC times, bank outcome and reason, the review
  text shown (fields and rendered wording with a version) and the acceptance time, acting cardholder
  name, confirm attempts, balance owed immediately before and immediately after the settled amount
  is applied, simulated-outcome flag); refused references
  with the same shown-and-accepted data and the refusal reason; current account ledger entries
  (reference, amount, time) kept by the fake adapter. Payments are append-only apart from their
  state changes, which are recorded, never overwritten. Retention (CD-5, CD-8): payment and acceptance records are kept for 6 years from the
  payment's final state; a refused reference is kept for 6 years from its refusal; a pending
  payment is kept until it settles and then 6 years; not tied to the card's life; no product path
  deletes them in this slice (OB-9). The rendered wording is stored
  with its version (v1 = §17). All data fictional; no real personal data.
- **Integrations:** outbound to the linked current account through a port: request a debit by
  reference and amount; ask the outcome of a reference. Expectations on the port: a debit request
  with a reference the bank has already seen is not applied twice; outcomes are complete, refused
  (with reason) or unknown (timeout); a status query by reference answers completed, refused or
  not received. The only adapter in this slice is the fake. Inbound: none new.
- **Analytics/observability needs:** one structured log event per payment state change (NFR-4).

## 9. Rollout
- No feature flag: the cardholder view is new and only reachable by its own routes; the admin
  view gains a balance figure and payment rows in history.
- Environments: local, CI, `demo` (deployed from main). The fake bank's steering control is
  available in every environment including `demo` (it is a lab product; the control is clearly
  labelled as the fake bank).
- Seed additions (fictional): one linked current account for the seeded business, display name
  "Brightline Supplies Ltd current account", account number shown as ending 7731 (the full
  fictional number lives only in the seed and is never displayed), opening available balance
  £25,000.00, with a standing authority for each seeded cardholder to repay their own card from
  it; no seeded payments and no seeded purchases (demo flow: simulate purchases, then
  pay). The cardholder view of each seeded card is reachable from the card list. Seed stays idempotent and identical across
  `tests/CardLab.TestKit/Seed.cs`, `appsettings.json` and `tests/e2e/src/seed.ts`.
- Schema: forward-only, additive migration. Rollback = redeploy previous green commit.
- Ticket: `CL-3` in every PR title and body.

## 10. Risks & assumptions
| Type | Statement | Impact if wrong | Mitigation / validation |
|------|-----------|-----------------|-------------------------|
| risk | The two-sided movement (external debit, local credit) is a classic place for duplicates or half-applied payments: a crash after the bank answered but before CardLab recorded it. | Money moved once, card still shows a balance (or the reverse). | R-6..R-11 and AC-9..AC-18; the port's by-reference status query is the recovery path; design must write the pending record before calling the bank. |
| risk | Holding the per-card lock while waiting on an external call (up to 10 s) collides with the 5 s lock timeout from 001. | Purchases on the card time out during a payment. | Design question for the next phase: the lock covers local steps only; the pending state (R-8) guards the card between them. Recorded as a design hint. |
| risk | Compliance wording decisions arrive late and change the review screen ACs. | Rework of AC-6/AC-7/AC-20 and the UI. | Pre-read runs after round 1; wording is settled before Gate 1 (NFR-7, AC-29). |
| assumption | One linked current account per business is enough; cardholders do not pick a source account. | Slice 2-4 need a source picker. | Model keeps the account as an entity with an id; the payment records the source account. |
| assumption | A balance that is simply approved purchases minus completed payments (no interest, no statements) is acceptable for a practice product. | Later slices add interest and statements and must reinterpret "balance". | Named explicitly in R-1 and the glossary; slice 2 forward constraint. |
| assumption | Register v1 has no entry for a party that initiates a debit from an account held at another provider (CardLab initiates, the bank provides the account); OB-1 is applied on the conservative CD-1 reading. Follow-up for the register owner (pre-read CQ-12), not a Gate 1 item while only the fake adapter exists. | A real bank adapter (later slice) may carry obligations not listed. | Register owner adds an entry before a real adapter is designed. |
| assumption | A seeded, named cardholder per card without login is acceptable as the acting person. | Real auth later changes how the actor is resolved. | Actor stays a port (`ICurrentActor` pattern from 001). |

## 11. Design hints (non-binding, from the human and the planner)
- The current account is an external system behind a port with a fake adapter that can complete,
  fail or time out (human). Suggested port shape: request debit by reference, query outcome by
  reference, both idempotent on the reference.
- Write the pending payment record before calling the bank, settle it after; never call the bank
  inside the per-card lock (001's 5 s lock timeout). The pending state is the guard between.
- The review screen's reference doubles as the exactly-once key (R-7).
- The fake adapter owns its own persisted ledger table so "both accounts show the transaction"
  survives restarts on the single F1 host; its steering control is a small labelled panel.
- Reuse 001's primitives (Money, Result, Builders, store contract suite, FakeApiClient).

## 12. Discovery
| ID | Question | Agent | Status | Report | Outcome (1 line) |
|----|----------|-------|--------|--------|------------------|

## 13. Decisions log
| # | Decision | Why | Delegated? |
|---|----------|-----|------------|
| 1 | Lane M. | Feature inside the existing architecture; all phases run, design may be short. | no (human) |
| 2 | Human's blind estimate for slice 1: M. Planner's independent estimate: L (see §16). | Recorded before any estimate from the planner, per the estimation gate. | no (human) |
| 3 | PR titles and bodies carry `CL-3`. | CI ticket-key check; the slices share one ticket. | no (human) |
| 4 | Proposed `risk_class: R2`. | First-of-kind money movement plus confirmation wording and records are R2 triggers in the register. | planner proposes; human confirms |
| 5 | Fee on this payment is £0.00 and is shown explicitly; an execution-time statement is shown before confirm. **Superseded in part:** the final strings (fee line, execution time, outcome messages) are the compliance partner's decisions CD-2 and CD-3 in the pre-read; the planner proposes strings, the ACs quote them once decided. | OB-1/OB-7: charges and execution time are given before the action even when zero, so slice 3 inherits the screen with a real fee. Customer-facing wording is not a planner call (pre-read CQ-10). | compliance partner (CD-2, CD-3) |
| 6 | Payment amount obeys the existing £0.01..£1,000,000.00 transaction bounds (R-17). | Keeps all money arithmetic inside the tested bounds; a larger balance is unreachable in the demo. | planner (delegated unless the human objects) |
| 7 | Purchases keep 001 semantics: no idempotency on the authorisation API; exactly-once applies to payments only. | 001 AC-32 stays valid; this slice adds a new kind of money movement rather than changing the old. | planner (delegated unless the human objects) |
| 8 | Q-1: balance is per card: approved purchases minus completed payments, floored at £0.00, computed from records. | Matches the brief and the per-card lock; property-testable. | no (human, round 1) |
| 9 | Q-2: every seeded card has a no-login cardholder view; the actor recorded is that card's cardholder. | Right person on every record; all cards demoable; slice 4 needs per-cardholder records. | no (human, round 1) |
| 10 | Q-3: balance changed between review and confirm: refuse and re-quote; nothing moves. | What was shown is exactly what is paid (OB-1/OB-7). | no (human, round 1) |
| 11 | Q-4: bank timeout: payment stays pending; cardholder checks status by reference; no new payment on the card until resolved; a pending payment is never re-sent as a new debit. | The only policy that can never double-debit. | no (human, round 1) |
| 12 | Q-5: a visible, labelled *Fake bank: next outcome* control in every environment including `demo`, plus a seeded available balance so refusals also happen naturally. | Failure and timeout paths can be shown live; zero run cost. | no (human, round 1) |
| 13 | Q-6: committed size at Gate 1 = L (human blind M, planner L). | Honest baseline for the estimation gate; lane stays M. | no (human, round 1) |
| 14 | Q-7: payment rows (kind = payment, with state) in the card's history (admin detail, cardholder recent activity) and the current account's recent activity on the cardholder view; cross-account history and notifications stay in slice 4. | "Both accounts show the transaction" is observable, not just claimed on a receipt. | no (human, round 1) |
| 15 | Fake bank steering is one-shot: the chosen outcome applies to the next debit request, then reverts to complete; timeouts come in two flavours (debit applied / not received) so both status-check outcomes are testable. | Safe demo default; AC-14 and AC-15 both reachable. | planner (delegated unless the human objects) |
| 16 | Seed values: current account "Brightline Supplies Ltd current account", number ending 7731, opening balance £25,000.00; no seeded purchases or payments. | Fictional, deterministic for E2E; a refusal is reachable by owing more than £25,000.00. | planner (delegated unless the human objects) |
| 17 | A refused or already-confirmed reference can never start a new payment; a new review mints a new reference. References are per card. | Closes the gap between exactly-once and re-quoting. | planner (delegated unless the human objects) |
| 18 | Payments do not write `AuditEvent` rows; the payment record with its state history, plus the structured log event, is the record. Audit stays "control changes only" (001 decision 12). | Keeps the audit promise crisp; the payment record is richer than an audit row anyway. | planner (delegated unless the human objects) |
| 19 | Recent-activity lists on the cardholder view show the last 10 entries, newest first. | Enough to see the payment land; full history is slice 4. | planner (delegated unless the human objects) |
| 20 | CQ-3: the execution-time statement must be true in the pending path ("normally within seconds; pending until the bank's outcome is known if it does not answer straight away"), not "immediately". | OB-1 maximum execution time; "immediately" was false under R-11. | planner, strings to CD-2 |
| 21 | CQ-5: refused confirms (balance changed) and repeated confirms are recorded against the reference. | OB-9 records sufficient to monitor; a refusal is a customer interaction with a shown quote. | planner (delegated unless the human objects) |
| 22 | CQ-6: the compliance record and AC-20 cover every payment state and refused references, not only completed payments. | R-14 said "each payment"; the AC was narrower. | planner |
| 23 | CQ-8: the fake bank control lives in its own labelled section of the cardholder view, never on the review screen or receipt; it changes only the next outcome; steered outcomes are marked simulated in the record. | Keeps the OB-7/OB-8 "one screen" evidence clean and records honest. | planner (delegated unless the human objects) |
| 24 | CQ-9: the record stores the settled amount and the balance owed around settlement (refined by decision 34: immediately before and immediately after); allocation by debt type is a slice 3 additive extension. | One rate and no interest today; sets the record shape without building allocation. | planner (delegated unless the human objects) |
| 25 | CQ-10: decision 5 moved to the compliance partner (CD-2/CD-3). | Customer-facing wording is a compliance decision; NFR-7/AC-29 already said so. | planner |
| 26 | Q-8 / CD-1: every business customer is treated as a micro-enterprise on a regulated credit agreement. | Strictest reading, one behaviour; OB-3..OB-6 apply from slice 2. | no (human as PM and compliance partner, round 2) |
| 27 | Q-9: cardholders act under a standing authority from the business to repay their own card from the linked account; recorded on the seeded link and named on each payment record. CardLab initiates, the bank provides the account. | Closes the authority gap (CQ-2) without an admin approval step. | no (human, round 2) |
| 28 | Q-10: the linked account charges nothing for this debit; "£0.00 (no fee)" means no fee from anyone. | Every charge is shown before confirm (OB-1/OB-8); slice 3 brings the real fee. | no (human, round 2) |
| 29 | Q-11 / CD-6: pending payments resolve by cardholder *Check status* and automatically when the cardholder view or admin card detail opens; effective time = bank's debit time if reported, else settle time. | Information in good time (OB-7) at the cost of one port call per page open. | no (human as PM and compliance partner, round 2) |
| 30 | CD-2, CD-3, CD-4, CD-7: fee as figure plus words, total, execution-time statement true in the pending path; plain-language outcome template (what happened / did money move / balance now / what to do next); record = structured fields + verbatim rendered text + wording version; review screen and record approved as the pattern for slices 2-4 once the strings are settled. Concrete strings: §17 (wording v1), approved by the partner in round 3 (Q-13) with From/To as placeholders. | Compliance partner decisions in `compliance-preread.md`. | no (human as compliance partner, round 2) |
| 31 | CD-5: retention is 6 years from the payment's final state, not tied to card life; no product path deletes records in this slice (Q-14). | Compliance partner decision; common UK horizon for financial records. | no (human as compliance partner, rounds 2-3) |
| 32 | Q-12: delegated details 15-19, 21, 23, 24 accepted. | Smallest testable choices. | no (human, round 2) |
| 34 | RUN 2 / CQ-11: the record stores the balance owed immediately before and immediately after the settled amount is applied (not a single "balance at settlement"). | Deterministic AC-20/AC-36; what slice 3 allocation will need. | no (human, gate changes) |
| 35 | RUN 2 / CD-8: refused references are kept 6 years from the refusal; a pending payment is kept until it settles, then 6 years from its final state. | Same period as CD-5 with a clear start event. | no (human as compliance partner, gate changes) |
| 36 | RUN 2 / B2: OB-3 applies now under CD-1 and is met trivially (one balance, one rate, no interest); no allocation logic is built. | Register scope under the chosen cohort. | no (compliance pre-read RUN 2, accepted by the human) |
| 33 | Q-13: wording v1 (W-1..W-10) approved as the final customer-facing text, with W-1's From/To lines as `{account}` / `{card}` placeholders so one screen serves every card. | Compliance partner decision (CD-2, CD-3); the ACs quote the approved text. | no (human as compliance partner, round 3) |

## 14. Open questions
| ID | Question | Blocked by | Owner |
|----|----------|------------|-------|

## 15. Glossary
| Term | Meaning |
|------|---------|
| Balance owed | What a card owes: approved purchases minus completed payments, never below £0.00. No interest or statements in this slice. |
| Linked current account | The business's own bank account from which card balances are paid; an external system behind a port, a fake in this slice. |
| Payment | A movement of money from the linked current account to a card, reducing the balance owed. Kind `full` in this slice. |
| Reference | The identifier created when the review screen is shown; identifies the payment to the cardholder, the bank and the exactly-once rule. |
| Pending / completed / failed | The three payment states: sent and outcome unknown; money moved on both sides; nothing moved. |
| Review screen | The single confirmation screen showing amount, fee, total, execution time and accounts before the cardholder confirms. |
| Receipt | What the cardholder sees after confirming: reference, amount, fee, time, accounts, outcome. |
| Cardholder view | The no-login page for one seeded card from which its cardholder pays. |
| Fake bank | The fake adapter behind the current account port; can complete, refuse or time out on demand. |

## 16. T-shirt size
| Who | Size | Basis |
|-----|------|-------|
| Human (blind, before any planner estimate) | M | Given at feature creation. |
| Planner (independent) | L | New domain concept (balance, payment lifecycle, exactly-once and one-in-flight rules) with property tests; a new external port plus a fake adapter with its own persisted ledger and steering; additive migration and store changes; four to five new endpoints; a new cardholder web area (view, review, receipt, pending/status); compliance records and wording; concurrency, idempotency and E2E tests. Roughly seven to eight PRs after the skeleton, against 001's eleven which included the whole delivery loop. Borderline M/L; no infrastructure work. |
| Committed (Gate 1) | L | Human's decision in round 1 (Q-6). Recorded in [estimate.md](./estimate.md), checked by `tools/estimate_check.py`. |

Whole CL-3 ticket: XL, split into four slices (this is slice 1).

## 17. Customer-facing wording (version v1)
Approved by the compliance partner on 2026-11-07 (CD-2, CD-3; pre-read). Strings quoted by the ACs and stored verbatim with each record (CD-4). `{amount}`, `{balance}`,
`{reference}`, `{account}`, `{card}` are substituted; amounts render as `£1,234.56`. The plain-language
template of CD-3 (what happened / did money move / balance now / what to do next) applies to W-2..W-10.

| ID | Where | Text |
|----|-------|------|
| W-1 | Review screen (one screen with the confirm button) | Heading "Pay your full balance". Lines: "Amount: {amount}" · "Fee: £0.00 (no fee)" · "Total to pay: {amount}" · "When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice." · "From: {account}" · "To: {card}" (where {account} renders as e.g. "Brightline Supplies Ltd current account, ending 7731" and {card} as e.g. "Ops van card, ending 0001"). Button: "Confirm payment of {amount}". |
| W-2 | Receipt, completed | "Payment complete. {amount} has left your current account and paid your card. Your card balance is now {balance}. Reference {reference}." |
| W-3 | Receipt, refused: insufficient funds | "Payment not made. Your bank refused it because the account did not have enough money. No money has moved. Your card balance is still {balance}. Add money to the account, then try again. Reference {reference}." |
| W-4 | Receipt, refused: account unavailable | "Payment not made. Your bank could not process it right now. No money has moved. Your card balance is still {balance}. Try again later. Reference {reference}." |
| W-5 | Receipt and card view, pending | "Payment pending. Your bank has not confirmed this payment yet, so the money may or may not have left your account; we never take it twice. Your card balance shows {balance} until we know the outcome. Choose Check status to ask your bank now; we also check each time you open this page. Reference {reference}." |
| W-6 | Confirm refused: balance changed | "Payment not made. Your card balance changed while you were reviewing. No money has moved. Your card balance is now {balance}. Review the new amount and confirm again if you want to pay it." |
| W-7 | Card view, nothing to pay | "Nothing to pay. Your card balance is £0.00." |
| W-8 | Card view, balance above the limit | "This balance cannot be paid here. Payments above £1,000,000.00 are not available online. No money has moved. Your card balance is {balance}. Contact your account administrator." |
| W-9 | Card view, a payment is pending and a new one is attempted | "A payment of {amount} (reference {reference}) is still pending. You cannot make another payment until we know its outcome. Choose Check status." |
| W-10 | Status check unanswered | "Still pending. Your bank did not answer. No new payment has been requested and no money has been taken twice. Try Check status again shortly. Reference {reference}." |

## Change log
| Date | Change | Reason (BUG-/Q-/human) |
|------|--------|------------------------|
| 2026-11-07 | Draft created from the human's idea and the context scan; round 1 frontier Q-1..Q-7. | human (feature start) |
| 2026-11-07 | Gate changes after compliance RUN 2: B1 summary wording; B2 OB-3 applies now (§1, G-3, NFR-7, AC-36); B3/CD-8 retention clocks for refused and pending (R-14, §8); CQ-11 before/after balances (R-14, §8, AC-20, AC-36); N1-N6 (AC-6 exact From/To, AC-13 card view W-5, fee £0.00 on receipts in AC-12/13/35, AC-18 no-fee-after clause, AC-37 reset unavailable, AC-38 settle time, J-1 step 4); CQ-12 listed as a register follow-up; decisions 34-36. | compliance RUN 2 (human: GATE changes) |
| 2026-11-07 | Round 3 answers: wording v1 approved with W-1 From/To as placeholders (decision 33); retention 6 years (decision 31); Q-13, Q-14 closed; frontier empty. | Q-13, Q-14 (human as compliance partner) |
| 2026-11-07 | Round 2 answers: Q-8..Q-12 as recommended and CD-1..CD-7 decided (decisions 26-32); R-2, R-4, R-10..R-12, R-14 updated; §17 wording v1 added and quoted by the ACs; AC-34, AC-35 added; Q-13 (approve strings) and Q-14 (retention value) opened. | Q-8..Q-12, CD-1..CD-7 (human) |
| 2026-11-07 | Compliance pre-read RUN 1 folded in: CQ-3 (execution-time statement true in the pending path), CQ-5 (refused and repeated confirms recorded), CQ-6 (AC-20 covers all states), CQ-8 (control placement, simulated flag), CQ-9 (record shape), CQ-10 (wording to the compliance partner); AC-33 added; Q-8..Q-12 opened for CQ-1, CQ-2, CQ-4, CQ-7 and the delegated details. | compliance pre-read (CQ-1..CQ-10) |
| 2026-11-07 | Round 1 answers: Q-1..Q-7 all as recommended (decisions 8-14); delegated details 15-19 (one-shot steering, seed values, reference lifecycle, no audit rows, list length); AC-30..AC-32 added; committed size L. | Q-1..Q-7 (human) |
