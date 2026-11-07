# Bug & fix ledger: Full balance payment (CL-3, slice 1)

<!-- Append-only. Never delete a row; change Status. The SessionStart hook reminds you of every
row whose Status is new, triaged, approved, in-progress or deferred. -->

| BUG | Found by | Severity | AC/R | Title | Root-cause layer | Decision | Status | Resume at | Issue | Updated |
|-----|----------|----------|------|-------|------------------|----------|--------|-----------|-------|---------|
| BUG-001 | reviewer (PR-02, F3) | S3 | AC-3 | Store contract P-1 expects the test's own linked account id, which the shared SQL test database cannot return | plan | fix-now (option A) | in-progress | build (PR-02 fix round) | | 2026-11-07 |

<!-- Severity: S1 blocker · S2 major · S3 minor · S4 cosmetic
     Root-cause layer: requirements | design | plan | code | test-harness | environment | needs-debugger
     Decision: fix-now (option A/B) | defer | wont-fix, with the human's words in the section below
     Status: new → triaged → approved → in-progress → fixed → verified   |  deferred  |  wont-fix -->

## BUG-001
**Repro:** PR-02 (#36) contract `PaymentStoreContractTests` P-1 asserts `GetLinkedAccountAsync()` returns the account passed to `CreateStoreAsync`, including its id.
**Expected / actual:** the suite must pass unchanged against PR-05's SQL adapter. On SQL each test class shares one database holding the seeded linked account plus one per `CreateStoreAsync`, and the port has no card or business scope, so the read cannot return "the test's" account deterministically.
**Evidence:** reviews/PR-02.review.md F3 (`Contracts/PaymentStoreContractTests.cs:38-50`, `ICardStore.cs:63-67`, `SqlTestDatabase.cs:57-64`).
### Triage (reviewer)
Root cause: plan. The PR-02 brief's P-1 "Then" (same id) does not fit an unscoped "the linked account" read on a shared test database. Not a coding error in PR-02.
Options: (A) P-1 asserts the fields every row shares (builder defaults = seed: DisplayName, LastFour, BankRef, AuthorityBasis) plus a non-empty id; amend P-1 in prs/PR-02.md. (B) PR-05 gets a dedicated SQL test class/database for P-1 (plan work). (C) scope the port by card (design change).
### Decision (human, 2026-11-07)
"A. Loosen P-1 (Recommended)": P-1 asserts the fields every linked-account row shares (DisplayName, LastFour 7731, BankRef = Seed.BankAccountRef, AuthorityBasis) plus a non-empty id. The P-1 rule lives in the PR-02-owned stub comment of `PaymentStoreContractTests.cs`, so the PR-02 coder changes comment and test together; no brief text names the id.
### Resolution
…
