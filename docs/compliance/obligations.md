---
version: v1
owner: compliance (CODEOWNERS: /docs/compliance/)
source: public UK rules, read 6 Nov 2026 from the FCA Handbook PDFs and legislation.gov.uk. Practice register, not legal advice.
---
# Obligations register (practice)

## R2 triggers
- A fee or charge shown to the customer, or a change to when it is shown
- Customer-facing disclosure or confirmation wording
- Payment allocation or minimum-payment logic
- Behaviour that differs for regulated credit agreements
- Records of what the customer saw and accepted
- First-of-kind: a money movement type the product has not offered before

## Entries
| ID | Rule | Quote / summary | Applies when | Typical acceptance criterion |
|----|------|-----------------|--------------|------------------------------|
| OB-1 | PSRs 2017 reg 52 | Under a framework contract, at the payer's request before an individual transaction: maximum execution time, charges payable, breakdown of charges. | Payment from a payment account under a framework contract | Charges and execution time available before confirm |
| OB-2 | CONC 4.2.15R(1)(a) | Pre-contract explanation for credit tokens covers different charges for different elements, e.g. a higher cost of withdrawing cash. | Regulated credit agreements; pre-contract | Product docs, not per-transaction; check agreement terms state the cash fee |
| OB-3 | CONC 6.7.4R | Allocate a repayment first to the debt at the highest rate of interest, then the next highest. | Card repayment, regulated agreements | Part payment allocated highest-rate first; property test |
| OB-4 | CONC 6.7.5R(1) | Minimum payment at least interest, fees and charges applied plus 1% of the amount outstanding. | Regulated agreements made on/after 1 Apr 2011 | "Minimum" option computes at least this amount |
| OB-5 | CONC 6.7.6R | Customer may choose to pay any amount between minimum and full balance (automated repayments). | Regulated agreements | Custom amount option between minimum and full balance |
| OB-6 | CONC 6.7.3BG(3) | Paying only the minimum is not by itself a sign of difficulty; a drop from above-minimum to minimum with spending unchanged may be. | Regulated agreements; MI | Event recorded so monitoring can see minimum-only patterns |
| OB-7 | PRIN 2A.5.3R / 2A.5.5R | Communications equip customers to make effective, timely, properly informed decisions; information given in good time. | Retail customers incl. micro-enterprises for payments | Fee and total shown on the confirm screen, before the action |
| OB-8 | FCA fair value review, 29 Oct 2026 (good and poor practice) | Poor practice: fees disclosed late or piecemeal. Good practice: tracking repeat charges, testing customer understanding. | Consumer finance products under the Duty | One fee screen; no fee revealed after confirm |
| OB-9 | SYSC 9.1.1R / 9.1.5G | Orderly records sufficient for the FCA to monitor compliance; retain as long as relevant. | All | Quote, fee shown and acceptance stored with the transfer; retention set by Compliance |
