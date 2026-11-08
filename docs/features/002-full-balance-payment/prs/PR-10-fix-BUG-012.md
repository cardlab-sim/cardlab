---
pr: PR-10
feature: 002-full-balance-payment
title: "Fix BUG-012: a rebuilt receipt shows the settlement balance, not today's balance"
wave: 6
depends_on: [PR-09]
branch: pr/002-full-balance-payment/10-fix-bug-012
base: feat/002-full-balance-payment
---

# PR-10: Fix BUG-012, a rebuilt receipt shows the settlement balance

Source: `bugs.md` BUG-012 (validator repro, reviewer triage, human decision option A). Read the BUG-012 section first.

## Goal
After a payment settles, a later purchase changes the card's balance. Every later rebuild of that payment's receipt (a
repeated confirm, `GET` of the receipt, a check on an already settled payment, the joiner fallback, `SettleAsync` finding
the payment already settled) currently passes the card's *current* owed into `BuildReceipt`. So a paid-in-full receipt
later reads "Your card balance is now £20.00". R-7 and AC-9 require the original receipt.

Rule (design §5 erratum BUG-012, PR-04 brief corrected): `Receipt.BalanceOwed`, and the `{balance}` in W-2..W-4, is the
settlement's `BalanceAfter` once the payment is settled, on every rebuild. While the payment is Pending it is the card's
current owed (W-5: "shows {balance} until we know the outcome"; this matches the cardholder view).

Fix it in one place. `BuildReceipt` (`PaymentService.cs:294-313`) uses `payment.Settlement?.BalanceAfter ?? pendingBalance`.
Rename the parameter to say it is the pending balance, and fix the doc comment. `StoredReceiptAsync` (`:343-347`) reads
`GetBalanceAsync` only while Pending. Call sites `:170`, `:244`, `:284`, `:379` and `:497-498` then need no other change.
Check each one still compiles to the same rule. No schema or web change: the SPA shows the server message verbatim.

## Acceptance criteria covered
| AC | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC-9 | R-7 | a confirmation that completed, then a later card purchase | the same reference is confirmed again, or the receipt reloaded | the response is the original receipt (balance £0.00) |
| AC-36 | R-1, R-11 | a pending payment settled by a check, then a later purchase | the receipt is reloaded | it keeps the settlement's balance |
| AC-12 / AC-13 | R-10, R-11 | failed / pending receipts | reloaded after a later purchase | failed keeps its settlement balance; pending shows the current owed |

## Files you own (edit only these)
| File | What to do |
|------|------------|
| src/CardLab.Domain/Application/PaymentService.cs | the rule in `BuildReceipt` and `StoredReceiptAsync` |
| tests/CardLab.Domain.Tests/Application/PaymentServiceTests.cs | tests 1-3 (T-U-38 amended) |
| tests/CardLab.Domain.Tests/Application/PaymentStatusCheckTests.cs | tests 4-5 |
| tests/CardLab.Integration.Tests/Payments/IdempotencyTests.cs | test 6 (T-I-4b) |

If you need another file, STOP and report BLOCKED.

## Tests (write first; each RED on the current code unless noted)
| # | Test | File | Then |
|---|------|------|------|
| 1 | T-U-38 `Receipt_reload_reads_without_calling_the_bank`, amended (`:648`) | PaymentServiceTests.cs | `Assert.Equal(original, later.Value)`; `BalanceOwed == Money.Zero`; message contains "Your card balance is now £0.00." (replaces the £10.00 assertion, which pinned the wrong rule) |
| 2 | `Repeated_confirm_after_a_later_purchase_returns_the_original_receipt` | PaymentServiceTests.cs | confirm, `AddPurchaseAsync("20.00")`, confirm again → `original with { Repeated = true }`, `BalanceOwed == Money.Zero` |
| 3 | `Failed_receipt_reload_after_a_later_purchase_keeps_its_balance` | PaymentServiceTests.cs | `NextDebit(InsufficientFunds)` (W-3, £150.50), `AddPurchaseAsync("20.00")` → `GetReceiptAsync` and `CheckStatusAsync` equal the original; `BalanceOwed == £150.50`; message contains "still £150.50" |
| 4 | `Receipt_of_a_payment_settled_by_check_keeps_the_settlement_balance` | PaymentStatusCheckTests.cs | as T-U-21 (pending, +£20.00, check → £20.00), then `AddPurchaseAsync("5.00")` → `GetReceiptAsync` and a second check equal the check's receipt; `BalanceOwed == £20.00` |
| 5 | `Pending_receipt_shows_the_current_balance` | PaymentStatusCheckTests.cs | pending, `AddPurchaseAsync("20.00")`, `GetReceiptAsync` → Pending, `BalanceOwed == £170.50`, message contains "Your card balance shows £170.50". Green before and after: it pins the pending rule on purpose |
| 6 | `Repeat_and_reload_after_a_later_purchase_return_the_original_receipt` (T-I-4b) | IdempotencyTests.cs | over HTTP: first confirm, `SimulatePurchaseAsync(20.00)` → repeat equals `first.Body with { Repeated = true }`, reload equals `first.Body`, `balanceOwed == "0.00"` |

## Mutations to report (each must make tests fail after the fix; revert after)
- M1: `BuildReceipt` ignores `Settlement` and uses the passed balance → tests 1, 2, 3, 4 and 6 fail.
- M2: `?? quote.Amount` instead of the pending balance → test 5 fails.
- M3: `BalanceBefore` instead of `BalanceAfter` → tests 1, 2, 4 and 6 fail.

## Gate
`commands` from docs/sdlc/project.json: build (`-warnaserror`), unit, integration (LocalDB), lint (`dotnet format`). No web or
E2E change is expected. If one is needed, report BLOCKED. PR title "CL-3 PR-10: <title>", body starts "Ticket: CL-3" and
names BUG-012.
