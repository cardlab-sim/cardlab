# ADR-005: Test harness: pure domain with property tests, ports with fakes and a shared contract suite

- **Status:** accepted (feature 001-card-controls, design DEC-7, DEC-15, DEC-16, DEC-20, §15,
  2026-11-04)
- **Context:** Money and limit logic is the high-risk path and must be property-tested (NFR-2);
  later features must be able to add tests without inventing harness.
- **Decision:** three layers with strict boundaries. *Unit*: domain modules through static
  functions (no I/O, no clock), FsCheck.Xunit.v3 properties with generators in
  `tests/CardLab.Domain.Tests/Generators/` (money, categories, periods, Europe/London instants
  across DST); application services with fakes from `tests/CardLab.TestKit` (`InMemoryCardStore`,
  `FakeClock`, `FakeCardNumberLookup`, `FakeActor`, `CapturingLogger`). *Integration*: `SqlCardStore`
  against a real SQL Server (fresh database per test class), the API through
  `WebApplicationFactory`, an in-memory OpenTelemetry log exporter, a latency test. One abstract
  `CardStoreContractTests` runs against both the fake and the SQL adapter. *E2E*: Playwright page
  objects, axe and keyboard helpers, a `PanScanner`, a test-only reset endpoint
  (`CardLab:TestEndpoints`), projects `local` and `smoke`. Web unit tests use Vitest + Testing
  Library with a `FakeApiClient`. Period arithmetic uses built-in `TimeZoneInfo`.
- **Alternatives:** mocks instead of fakes (brittle, no contract); NodaTime; Testcontainers;
  journeys tolerant of leftover state.
- **Consequences:** fakes cannot drift from the real adapter; tests are cheap to add by following
  the extension guide in the design (§15.4); the reviewer enforces the boundary table.
