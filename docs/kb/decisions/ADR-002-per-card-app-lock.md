# ADR-002: Serialise all writes to a card behind a per-card application lock

- **Status:** accepted (feature 001-card-controls, design DEC-4, 2026-11-04)
- **Context:** Concurrent authorisation requests for the same card must be decided as if
  sequential: approved spend in a period never exceeds a limit (R-12, AC-21). Limit changes must
  not interleave with decisions (R-13).
- **Decision:** every write to a card (authorisation, freeze/unfreeze, limit set/remove) runs
  through `ICardStore.WithCardLockAsync(cardId, work)`. The SQL adapter opens a transaction, calls
  `sp_getapplock @Resource='card:{id}', @LockMode='Exclusive', @LockOwner='Transaction',
  @LockTimeout=5000`, runs the work (read state, `SUM` approved spend in the period's UTC range,
  decide, append rows) and commits. Spend is always computed from the transaction rows; there are
  no counters to keep consistent.
- **Alternatives:** materialised spend buckets with optimistic concurrency (second source of
  truth, retries); SERIALIZABLE isolation with deadlock retry (nondeterministic under bursts).
- **Consequences:** authorisations on one card are serialised (a card is one person); the adapter
  is SQL Server specific while the port stays portable; the in-memory fake uses a per-card
  semaphore and both adapters pass the same contract suite.
