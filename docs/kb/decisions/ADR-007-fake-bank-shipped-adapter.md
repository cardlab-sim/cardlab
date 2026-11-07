# ADR-007: The fake bank is a shipped adapter with a persisted ledger and a one-shot steering control

- **Status:** accepted (feature 002-full-balance-payment, design DEC-6, DEC-7, DEC-14, DEC-15, 2026-11-07)
- **Context:** The linked current account is an external system, but CardLab is a lab product
  with no real bank. Failure and timeout paths must be demonstrable in every environment including
  `demo` (requirements decision 12), both accounts must show a payment across restarts (R-15), and
  the fake's outcomes must be distinguishable from real ones in the compliance record.
- **Decision:** `SqlFakeBank : ICurrentAccountBank` lives in `CardLab.Infrastructure.FakeBank`
  with its own `Bank*` tables in the same database and DbContext, never joined with card tables
  (reviewer-enforced boundary, as ADR-001 does for money code). It is idempotent on the payment
  reference, refuses debits above the available balance, and in its two timeout modes never
  returns (the caller's budget token cancels it): "debit applied" posts the ledger entry first,
  "not received" records nothing, so the status query later answers `Completed` or `NotReceived`.
  A single `BankSteering` row holds two one-shot settings (next debit outcome; next status check
  answers / does not answer) exposed at `/api/fake-bank` and on the cardholder view's own "Fake
  bank" section, never on the review screen or receipt; steered outcomes carry `Simulated = true`
  into the record. The deploy smoke pays once per deploy on `demo` with the bank steered to
  *complete* and the header `X-CardLab-Origin: deploy-smoke`, stored as `Origin = DeploySmoke` (wire `deploy_smoke`; BUG-005)
  on the quote, so smoke records are never mistaken for a person's.
- **Alternatives:** returning "unknown" instantly in timeout mode (budget path never exercised);
  a separate project and DbContext (a day more for a boundary review can enforce); in-memory or
  file-backed ledger (lost on restart or fragile on App Service).
- **Consequences:** one E2E spec waits a real 10 s; steering is global (one row), acceptable for
  a lab; a real adapter later replaces the class behind the same port and contract suite, and the
  fake moves to a test/demo project.
