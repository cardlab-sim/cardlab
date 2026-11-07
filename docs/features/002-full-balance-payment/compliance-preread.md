---
feature: 002-full-balance-payment
register_version: v1
run: 2
proposed_class: R2
---
# Compliance pre-read: CL-3 slice 1, pay the full card balance from the linked current account
> First-line aid. Not a clearance. Decisions below are for the compliance partner.

Read against `docs/compliance/obligations.md` version v1 and the Gate 1 candidate of
`01-requirements.md` (status draft, risk_class R2, §17 wording v1 W-1..W-10, decisions 1-33).

**Proposed class R2 (unchanged).** Register R2 triggers hit: first-of-kind money movement; a fee
shown to the customer (£0.00, shown explicitly); customer-facing confirmation wording (W-1..W-10);
records of what the customer saw and accepted (R-14). No new trigger since RUN 1: there is still no
allocation or minimum-payment logic (OB-3 is met trivially, see below), and CD-1 gives one behaviour
for every customer, so nothing "differs for regulated credit agreements". CD-7 approves this screen
and record as the pattern later slices may cite; that does not lower this slice.

## Changes since RUN 1
- CD-1..CD-7 are decided (words below unchanged). CQ-1..CQ-10 are resolved in the doc (see the
  questions table for where).
- **OB-3 moves into scope.** Under CD-1 (every customer on a regulated agreement) its applies-when,
  "Card repayment, regulated agreements", is met by this slice. RUN 1 placed it in "from slice 2";
  that was too narrow. It is met trivially (one balance, one rate, no interest), but the doc's §1
  scope sentence, G-3 and NFR-7 still list it as not applying now, and the J-3 edge (a purchase
  approved while a payment is pending, so the payment settles less than the balance) has no AC.
- New: CQ-11 (what "balance owed at settlement" means), CQ-12 (register gap on the initiator role),
  CD-8 (retention clock for refused references, which are not payments; open).
- The doc summary still says "The money moves immediately", which CQ-3 / decision 20 / CD-2 replaced.

## Obligations touched → acceptance criteria
| ID | Register | Rule (quoted) | Touches (R-/J-) | Proposed AC (Given/When/Then) |
|----|----------|---------------|-----------------|-------------------------------|
| OB-1 | PSRs 2017 reg 52 | "Under a framework contract, at the payer's request before an individual transaction: maximum execution time, charges payable, breakdown of charges." Applies when: "Payment from a payment account under a framework contract". Scope: kept in on the CD-1 reading. CardLab initiates the debit and the external bank provides the account (decision 27); register v1 has no entry for the initiator role (CQ-12). | R-4, R-12, J-1, J-3 | **Covered by AC-6 and AC-7.** AC-6: the review screen shows W-1 exactly ("Fee: £0.00 (no fee)", "Total to pay: {amount}", "When: normally within seconds of confirming. If your bank does not answer straight away, this payment shows as pending until we know the outcome. We never take the money twice.") on the same screen as "Confirm payment of {amount}". AC-7: the receipt repeats fee £0.00. W-1 gives a normal time plus the pending path rather than a numeric maximum. That is CD-2 as decided ("pending-true time"), chosen over "a stated maximum". Testability fix N1. |
| OB-3 | CONC 6.7.4R | "Allocate a repayment first to the debt at the highest rate of interest, then the next highest." Applies when: "Card repayment, regulated agreements". Met under CD-1. This slice has one undifferentiated balance at one rate with no interest, so any settled amount reduces that single balance. No allocation logic is built (this is not the "payment allocation" R2 trigger). | R-1, R-11, R-14, J-3, §4 slice 3 | **Partly covered** by AC-3 and AC-25 (balance arithmetic). Missing: the J-3 edge where the payment settles less than the balance. Proposed **AC-36**: **Given** a pending payment of £150.50 on the Ops van card (AC-13), then a purchase of £20.00 approved on that card, **When** the bank reports the payment completed and the status is checked, **Then** the balance owed is £20.00, and the payment record shows a settled amount of £150.50 with the balance owed £170.50 before and £20.00 after settlement (per CQ-11). |
| OB-7 | PRIN 2A.5.3R / 2A.5.5R | "Communications equip customers to make effective, timely, properly informed decisions; information given in good time." Applies when: "Retail customers incl. micro-enterprises for payments". Met under CD-1. | R-3, R-4, R-5, R-8, R-10, R-11, R-12, R-17, J-1, J-3 | **Covered.** AC-6 (fee and total on one screen with the confirm action, "not behind a link or later step"). Each outcome follows the CD-3 template and quotes §17: AC-5 W-7, AC-7 W-2, AC-8 W-6, AC-11 W-9, AC-12 W-3, AC-13 W-5, AC-16 W-10, AC-24 W-8, AC-35 W-4. AC-34 is the auto-check on open (CD-6). Gap: W-5 is approved for "Receipt and card view", but no AC asserts it on the card view (fix N2). |
| OB-8 | FCA fair value review, 29 Oct 2026 | "Poor practice: fees disclosed late or piecemeal." Applies when: "Consumer finance products under the Duty". Met under CD-1. | R-4, R-10, R-11, R-12, R-13, J-1, J-2, J-3 | **Covered.** AC-6 (one fee screen); AC-7 (receipt fee £0.00); AC-12, AC-15, AC-31 (no balance change on failure); W-2..W-10 add no fee or condition beyond W-1. Gap: R-12 says every receipt shows the fee, but AC-12, AC-13 and AC-35 do not assert it. No AC asserts that no charge entry appears anywhere after confirm (fix N3). |
| OB-9 | SYSC 9.1.1R / 9.1.5G | "Orderly records sufficient for the FCA to monitor compliance; retain as long as relevant." Applies when: "All". | R-5, R-6, R-7, R-14, R-15, R-16, §8, J-1, J-2, J-3 | **Covered.** AC-20 covers every state and refused references: fields + verbatim wording + version (CD-4), acceptance time, actor, authority basis, confirm attempts, bank outcomes, simulated flag, and an unchanged shown-and-accepted part. Also AC-8, AC-9, AC-30 (refused and repeated confirms), AC-17 (append-only state history) and AC-33 (simulated flag). Retention: 6 years from the payment's final state (CD-5) in R-14 and §8. Gaps: the retention clock for refused references (CD-8, open), the meaning of "balance owed at settlement" (CQ-11), and no AC that the reset is unavailable where `CardLab:TestEndpoints` is off (fix N4). |

## Questions for the grilling
| ID | Question for the PM | Why (register entry or gap) |
|----|---------------------|-----------------------------|
| CQ-1 | Customer cohort and regulated status. | **Resolved:** CD-1, decision 26, §1 constraints. |
| CQ-2 | Authority to debit the business account; who is the provider. | **Resolved:** decision 27, R-2, R-14 (authority basis on each record), AC-20. Residual register gap in CQ-12. |
| CQ-3 | True execution time; "immediately" vs pending. | **Resolved:** decision 20, R-4, W-1, J-1 step 4. One stale sentence remains in the summary (blocker B1). |
| CQ-4 | Can the bank charge for the debit? | **Resolved:** decision 28, R-4 ("no fee from CardLab or the bank"). |
| CQ-5 | Record refused and repeated confirms? | **Resolved:** decision 21, R-5, R-7, R-14, AC-8, AC-9, AC-30. |
| CQ-6 | Record and AC-20 for every state? | **Resolved:** decision 22, R-14, AC-20. |
| CQ-7 | Other resolution paths; effective time. | **Resolved:** decision 29, CD-6, R-11, W-5, AC-34. AC-34 tests only the "bank reports a debit time" branch. The "otherwise the settle time" branch has no AC (minor, fix N5). |
| CQ-8 | Simulator placement; simulated marking. | **Resolved:** decision 23, R-15, AC-33. |
| CQ-9 | Record shape for later allocation. | **Resolved:** decision 24, §4 slice 3, R-14. The term it introduced needs a definition (CQ-11). |
| CQ-10 | Move wording from the planner to the partner. | **Resolved:** decisions 5 (superseded) and 25, §17 approved under CD-2/CD-3. |
| CQ-11 | R-14 and §8 store "the balance owed at settlement". Is that the balance immediately before the settled amount is applied, or immediately after? Recommend storing both. In the J-3 example these are £170.50 and £20.00. | OB-9 (records sufficient to monitor) and OB-3 (slice 3 must show what the amount was applied to). AC-20 cannot be checked deterministically while the term is undefined. A fact, not a judgement. |
| CQ-12 | For the register owner, no doc change: register v1 has no entry for a party that initiates a debit from an account held at another provider (CardLab initiates, the bank provides the account). Should the register gain one before a real bank adapter exists? | Gap. OB-1 is kept on the conservative CD-1 reading; nothing here can be cited beyond it. Not a Gate 1 item for this slice (fake adapter only). |

## Decisions for the compliance partner
| ID | Decision needed | Options | Recommended | Status (open/decided) | Decided by, words |
|----|-----------------|---------|-------------|-----------------------|-------------------|
| CD-1 | Which customer cohorts are in scope, and so which entries apply: OB-1, OB-7, OB-8 now; OB-3..OB-6 from slice 2. | (a) Treat every business customer as a micro-enterprise on a regulated agreement (strictest; one behaviour for all). (b) Companies only, unregulated: OB-3..OB-6 out, OB-7/OB-8 doubtful. (c) Mixed cohorts with a per-account flag and different behaviour. | (a): one behaviour, no "differs for regulated agreements" trigger, upgrades are free; revisit if the cohort answer to CQ-1 says otherwise. | decided | human via relay, 2026-11-07: "Strictest for all (Recommended)" |
| CD-2 | Review screen wording: fee line, total, execution-time statement, source and destination. | Fee: "No fee" / "Fee £0.00 (no fee)" / "No CardLab fee; your bank may charge" (per CQ-4). Execution time: "immediately when you confirm" / "usually within seconds; if your bank does not answer straight away the payment shows as pending until it does" / a stated maximum. | Show the fee as a £0.00 figure with words, plus the total, and an execution-time statement that is also true in the pending path (second option), so slice 3 can drop in a real fee without changing the layout. | decided | human via relay, 2026-11-07: "£0.00 fee + pending-true time (Recommended)" |
| CD-3 | Outcome wording: completed, refused (each reason), pending ("money will not move twice"), balance changed, nothing to pay, over £1,000,000.00 limit. | Draft wording as written in R-5, R-10, R-11, R-17 / partner-supplied wording / plain-language template: what happened, did money move, balance now, what to do next. | Plain-language template, one sentence per element; the partner approves the final strings, which the ACs then quote. | decided | human via relay, 2026-11-07: "Plain-language template (Recommended)"; wording v1 (W-1..W-10) approved 2026-11-07 (Q-13) with W-1 From/To as {account}/{card} placeholders |
| CD-4 | Form of the "what was shown and accepted" record. | (a) Structured fields only (amount, fee, total, accounts, execution-time code). (b) The rendered wording stored verbatim. (c) Both, plus a wording version identifier. | (c): proves exactly what was displayed and stays queryable for slice 4 history. | decided | human via relay, 2026-11-07: "Fields + verbatim text + version (Recommended)" |
| CD-5 | Retention period for payment and acceptance records. | Life of the card (draft) / life of the business account plus a fixed period / fixed period from the payment date. | A period the partner sets, not tied to the card's life; in this slice no product path deletes records (the test reset is off on `demo`). | decided | human via relay, 2026-11-07: "Partner-set fixed period (Recommended)"; period set 2026-11-07 (Q-14): "6 years from the payment's final state" |
| CD-6 | Policy for a pending payment (first-of-kind): is a cardholder-only *Check status* acceptable, given money may have left the current account before the card shows it? | (a) Cardholder check only (draft). (b) Also check automatically when the card view opens. (c) Also an admin resolution path. | Partner's call; (b) shortens the gap at little cost and stays inside slice scope. | decided | human via relay, 2026-11-07: "Cardholder + auto check on open (Recommended)" |
| CD-7 | Accept the review screen plus acceptance record as the approved pattern that slices 2-4 reuse (so later slices can cite it when proposing a class). | Approve as pattern / approve for this slice only. | Approve as pattern once CD-2..CD-4 are decided. A non-zero fee (slice 3) stays an R2 trigger anyway. | decided | human via relay, 2026-11-07: "Approve as pattern (Recommended)" |
| CD-8 | Retention clock for records that are not payments. CD-5 runs "from the payment's final state", but a refused reference (R-5, R-7, AC-8, AC-30) never becomes a payment, though R-14 keeps what was shown and accepted for it. Also confirm that a payment still pending has no final state and so is kept until it settles. | (a) Refused reference: 6 years from the refusal (its final state); pending: kept until settled, then 6 years. (b) Refused reference: 6 years from the last confirm attempt on it. (c) Another period the partner sets. | (a): same period as CD-5, a clear start event, and no extra rule. | decided | human via relay, 2026-11-07: "6y from refusal; pending kept until settled (Recommended)" |

Note on CD-1: its "Decision needed" text (written in RUN 1) lists OB-3 under "from slice 2". With the cohort decided as (a), OB-3's applies-when is met now (see the obligations table). The decision and its words are unchanged. Only the list of entries that follows from it gains OB-3, met trivially.

## Why entries did not apply
- OB-2: a pre-contract explanation of credit-token charges (e.g. a cash fee). It is a product document, not a per-transaction screen, and no cash withdrawal or agreement terms change in this slice (slice 3).
- OB-4: minimum-payment calculation. There are no statements and no minimum option in this slice (slice 2).
- OB-5: any amount between minimum and full for automated repayments. This slice is a one-off manual full payment (forward constraint for slice 2, already in §4).
- OB-6: monitoring of minimum-only patterns. There are no minimum payments in this slice; the payment `kind` the record keeps is what slice 2 needs.

## Gate 1 items (for the main thread)
Blockers (the doc contradicts a decision, omits an applicable entry, or leaves a decision open):
- **B1** (CQ-3 / CD-2). The summary paragraph under the title says "The money moves immediately". Replace "The money moves immediately, both accounts show the movement," with "The money normally moves within seconds of confirming (if the bank does not answer straight away the payment shows as pending until its outcome is known), both accounts show the movement,".
- **B2** (OB-3 scope under CD-1). §1 constraints: replace "OB-1, OB-7, OB-8, OB-9 apply now and OB-3..OB-6 from slice 2." with "OB-1, OB-3, OB-7, OB-8, OB-9 apply now (OB-3 is met trivially: one balance at one rate, no interest) and OB-4..OB-6 from slice 2." In G-3 and NFR-7, change "OB-1, OB-7, OB-8, OB-9" / "OB-1, OB-7, OB-8 and OB-9" to include OB-3. Add AC-36 as proposed in the OB-3 row (Covers R-1, R-11, R-14).
- **B3** (CD-8 open; AC-29 and G-3 require zero open decisions). Once the partner decides, in R-14 replace "Retention is 6 years from the payment's final state (CD-5)" with "Retention is 6 years from the payment's final state, or for a refused reference from its refusal (CD-5, CD-8)" (wording per the option chosen), and make the same change in §8.

Non-blocking fixes (recommended in the same edit):
- **N1** AC-6: change the Given to "the seeded Ops van card (ending 0001) with balance owed £150.50". Replace "the To line rendering the card as e.g. "Ops van card, ending 0001"" with "the line "To: Ops van card, ending 0001"", and render the From line as "From: Brightline Supplies Ltd current account, ending 7731". With "e.g." the AC is not exact.
- **N2** AC-13 Then: append "; the card view shows the pending payment with W-5 of §17 and *Check status*".
- **N3** AC-12, AC-13, AC-35 Then: add "fee £0.00" to the receipt assertion. AC-18 Then: append "; no fee or charge entry appears on the receipt, either account's activity or the record other than the £0.00 shown on the review screen".
- **N4** AC-21: add a second case. Given `CardLab:TestEndpoints` off (as on `demo`), When the reset is called, Then it is unavailable and payment and acceptance records are unchanged. 001's DEC-16 is the existing pattern.
- **N5** AC-34: add a case where the bank reports completion without a debit time, so the effective time is the settle time (R-11).
- **N6** J-1 step 4: "fee (none)" becomes "fee (£0.00, no fee)", to match W-1.
- **N7** R-14 and §8 (after CQ-11): "the balance owed at settlement" becomes "the balance owed immediately before and immediately after the settled amount is applied".
