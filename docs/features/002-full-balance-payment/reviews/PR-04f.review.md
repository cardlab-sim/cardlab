# PR-04f review: Fix BUG-006, in-flight registry entry cleared before the confirm returns (mode `pr-light`)

- PR: https://github.com/cardlab-sim/cardlab/pull/41 · branch `pr/002-full-balance-payment/04f-fix-bug-006` @ `0f38fea` · base `feat/002-full-balance-payment`
- Inputs: `prs/PR-04-fix-BUG-006.md`, `bugs.md` BUG-006, `02-design-digest.md` DEC-5 (row 35, line 67), `02-tech-design.md` §5 row 386 and flow line 489, PR diff, PR body.

## Verdict: **APPROVE WITH FIXES**

Counts: blocker 0 · major 1 · minor 2 · nits (one line at the end).

The production fix is correct. `ConfirmAsync` now releases the entry before every completion it signals:
- lock #1 failure after registration: `PaymentService.cs:136`;
- step 2/3 failure: `PaymentService.cs:182`;
- success: `PaymentService.cs:187`.

The registry's own cleanup is identity-guarded (`InFlightPayments.cs:78-86`), so a late cleanup can no longer delete a newer registration.

The DEC-5 checks all hold:
- **Registered before step 1 commits.** Unchanged (`PaymentService.cs:362`, inside the lock #1 unit of work).
- **A repeat always finds the task while it runs.** A repeat reads the record and calls `TryGet` inside the card lock (`PaymentService.cs:343-370`). The owner releases only after `SettleWithLockAsync` has committed under that same lock (`:178`, `:431`/`:438`). So a repeat either holds the lock before the settle and finds the entry, or comes after the settle and reads the committed settled state. A repeat can never both miss the entry and miss the settled state.
- **A confirm arriving after Release never debits twice.** The payment row exists, so `ConfirmRules` returns `Repeat`, and that branch never calls the bank. On the failure and settle-lock-timeout paths the stored state is Pending. The repeat returns that Pending receipt, the same answer the joiner fallback gives (`:164-169`).
- **Joiners get the same outcome.** They hold the registry's `source.Task`. `RunAsync` completes it with the owner's result or exception after `completion` completes, and its guarded `Remove` is then a no-op.
- **The joiner's return.** Joiners return only after `source` completes. The owner has already released by then, so the brief's "when ConfirmAsync (or a joiner) returns, the reference is no longer registered" holds.
- **Status-check window (pre-existing, unchanged in kind).** `CheckStatusAsync` reads the record at `:210` and calls `TryGet` at `:217`, both outside the lock. A check can read Pending, then miss the entry released in between, then ask the bank's status. That debit has already been answered, so R-9 still holds, and `SettleAsync` re-reads under the lock and skips the update (`:487-490`). The same window existed before this PR, with the entry removed even later. This is not a finding.

The weak spot is the regression guard. No test fails deterministically if the service-side ordering is reverted (F1).

## Evidence (fresh, PR head `0f38fea` in a detached scratch worktree, since removed)
- Scope: `doc_lint.py scope` cannot parse this brief's owned-files table (the orchestrator's note). By hand, `git diff --stat origin/feat/002-full-balance-payment...origin/pr/002-full-balance-payment/04f-fix-bug-006` shows 3 files: `InFlightPayments.cs`, `PaymentService.cs`, `InFlightPaymentsTests.cs`. All three are owned. **PASS**
- `dotnet test tests/CardLab.Domain.Tests` → **279 passed, 0 failed, 0 skipped**.
- `dotnet build CardLab.sln -warnaserror` → **0 errors**. `dotnet format CardLab.sln --verify-no-changes` → **exit 0**. Web was not run because the PR touches no web files.
- Mutation M1: `Release` body emptied → `Entry_is_gone_when_the_start_task_completes` **failed 40/40**, and `Late_cleanup…` also fails. That result depends on thread-pool ordering; see F2.
- Mutation M2: `_inFlight.Release(reference);` deleted at `PaymentService.cs:187`, which reintroduces BUG-006 →
  - the brief's filter (`Client_disconnect_never_cancels|InFlightPayments`) **passed 40/40**;
  - the full suite **failed in 2 of 6 runs** (`Client_disconnect_never_cancels_the_debit_and_the_payment_settles`, line 382).
  So reverting the fix is caught only by the original flaky signal.
- Scratch variant of test 1 (the F2 fix: plain `TaskCompletionSource<Receipt>()`, observer sets a local synchronously) → green on the PR code, **red 20/20** under M1.

## Findings

| # | Severity | Where | Finding | Why it matters | Fix | Check |
|---|----------|-------|---------|----------------|-----|-------|
| F1 | major | `PaymentService.cs:136,182,187-188`; `InFlightPaymentsTests.cs:70-87` | The actual fix (release, then complete) lives in three pairs of calls in the service. No test pins their order. Test 1 calls `registry.Release` itself, so it only proves that `Release` deletes a dictionary key. It cannot see whether `ConfirmAsync` calls it before `SetResult`. Deleting `:187` reintroduces BUG-006 and the suite goes red only intermittently (M2: 0/40 filtered, 2/6 full). The brief's test 1 "Catches: removal racing the completion (BUG-006)" is therefore not met. The brief's second option exists so the registry can own this ordering. | The next edit to the confirm's completion paths can quietly bring back the CI flake this PR exists to remove. The 200-run stability loop proves the fixed code is stable, but it does not prove that a revert would be caught. | Make "remove, then complete" one registry operation. Add `public void Complete(PaymentReference reference, TaskCompletionSource<Receipt> completion, Receipt result)` (body: remove under `_gate`, then `completion.SetResult(result)`) and `public void Fail(PaymentReference reference, TaskCompletionSource<Receipt> completion, Exception error)` (remove, then `completion.TrySetException(error)`). Replace the three service pairs with `_inFlight.Fail(reference, completion, error)` / `_inFlight.Complete(reference, completion, result)`. Make `Release` private, or delete it. Rewrite test 1 against `Complete`, keeping the brief's name `Entry_is_gone_when_the_awaited_task_completes`: plain `new TaskCompletionSource<Receipt>()`; attach an `ExecuteSynchronously` continuation that sets a local `seen = registry.TryGet(First, out Task<Receipt>? ignored)` **before** `GetOrAdd`; call `registry.Complete(First, gate, ReceiptFor(First))`; `Assert.False(seen)`. Add the same test for `Fail`. | (a) Swap the two statements inside `Complete` → the new test fails on every run (20/20). (b) Same for `Fail`. (c) `grep -nE "completion\.(Try)?Set" src/CardLab.Domain/Application/PaymentService.cs` prints nothing. (d) `dotnet test tests/CardLab.Domain.Tests` is all green. |
| F2 | minor | `InFlightPaymentsTests.cs:73,77-81`; PR body "Tests added" | `gate` is created with `RunContinuationsAsynchronously`, which overrides `TaskContinuationOptions.ExecuteSynchronously`. The observer and `RunAsync`'s continuation are both queued to the thread pool, so test 1 is not deterministic, contrary to its comment and the PR body ("deterministic by attaching a synchronous observer"). It catches a no-op `Release` only because pool ordering happens to favour it (40/40 here; not guaranteed under CI load, where local queues are LIFO). | A test that is red only by scheduling luck in the mutant case is a weak guard, and the comment states something that is not true. | This is covered by the F1 rewrite. If F1 is not taken, use `new TaskCompletionSource<Receipt>()` without options, set a local `bool` synchronously in the continuation instead of the `seenRegistered` TCS, assert it right after `gate.SetResult`, and drop the `async`. | Empty the `Release` body → the test fails 20/20 runs (verified with the scratch variant). |
| F3 | minor | `02-tech-design.md:386` (§5 `InFlightPayments` row), `02-design-digest.md:67` | The documented interface is still only `Task<Receipt> GetOrAdd(...)`. The class now also exposes `TryGet` (since PR-04) and `Release`, or `Complete`/`Fail` after F1, and these carry the "cleared on completion" contract of DEC-5. | Docs and code have drifted. The next reader of §5 will not know that the owner must release before completing. | Orchestrator amends §5 row 386 and digest line 67 to list `TryGet` and `Complete`/`Fail` (or `Release`), with the one-line ordering rule "owner removes before completing" (doc change, not a design change). | `grep -n "TryGet" docs/features/002-full-balance-payment/02-tech-design.md` matches the §5 row. |

Nits:
- The test name differs from the brief (`…_start_task_…` vs `…_awaited_task_…`); fixed by F1.
- `Release` is unconditional while `Remove` is identity-guarded. That is harmless today because only one `Create` per reference can commit, and F1 makes it private.
- Pre-existing: an exception between the end of lock #1 and the `try` at `PaymentService.cs:174` (only the `PaymentConfirmAttempt` log at `:145` for the Create path) would leave the entry registered and joiners waiting forever. To close it, start the step 2/3 `try` before the `:143` log for the Create case.

No `upstream:` items.
