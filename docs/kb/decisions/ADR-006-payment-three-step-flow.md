# ADR-006: Money moves in three steps; the bank is never called inside the per-card lock

- **Status:** accepted (feature 002-full-balance-payment, design DEC-3, DEC-4, DEC-5, 2026-11-07)
- **Context:** A payment debits an external account (port `ICurrentAccountBank`, up to 10 s to
  answer) and credits the card balance (local). ADR-002 serialises card writes behind a 5 s
  application lock; holding it across the bank call would make purchases on that card time out.
  A payment must never be sent twice or applied on one side only (R-7, R-9).
- **Decision:** `PaymentService.ConfirmAsync` runs (1) inside `WithCardLockAsync`: validate the
  reference (the quote minted at review time), compare the balance to the quoted amount, require no
  pending payment, insert `Payment(Pending)` and commit; (2) outside any lock: `RequestDebitAsync`
  under a `CancellationTokenSource` budget (`CardLab:Payments:BankTimeoutSeconds`, 10 s); (3) inside
  the lock again: settle `Completed`/`Failed` with the balance before and after, or leave `Pending`
  when the bank did not answer. The pending state guards the card between steps (one in flight per
  card, also enforced by a filtered unique index). Recovery is the bank's by-reference status
  query (`GetDebitStatusAsync`), run on *Check status* and automatically when the cardholder view
  or admin card detail opens; a pending payment is never re-sent. Payment writes share the card's
  unit of work through `ICardWriter.Payments : IPaymentWriter`. Two confirms of one reference at
  the same instant: the second joins the first's in-flight task (`InFlightPayments`, in-process,
  single host per ADR-001) and returns the identical receipt. The bank budget is its own token, not
  linked to the request (a disconnect never cancels a debit, BUG-002); a status check awaits an
  in-flight confirm on this host within its own budget and never asks the bank meanwhile (BUG-003).
- **Alternatives:** hold the lock across the bank call (breaks ADR-002); outbox + background worker
  (receipt waits for a poll cycle; F1 has no Always On); separate payment store with its own lock
  (atomicity with the balance needs a shared transaction).
- **Consequences:** two short transactions per confirm; a crash between steps leaves a pending
  payment that the next open settles; the port contract requires idempotency on the reference;
  the in-flight join weakens to "stored receipt" on a second host, which the status check covers.
