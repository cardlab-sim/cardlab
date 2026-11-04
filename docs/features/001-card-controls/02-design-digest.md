---
feature: 001-card-controls
source: ./02-tech-design.md
design_approved_at: 2026-11-04T22:19Z
---

# Card controls: Design digest

Current design for downstream agents (planner, coders, reviewers, validator). Detail lives in
[02-tech-design.md](./02-tech-design.md) (cited as §n). Explainer:
[design/explainer.html](./design/explainer.html).

## Shape
- One ASP.NET Core (.NET 10 LTS) host serving the Minimal API and the React + TypeScript SPA
  (static files in `wwwroot/`, `MapFallbackToFile`), backed by SQL Server (LocalDB locally,
  mssql 2022 container in CI, Azure SQL free offer on `demo`). §1, §4.
- **Complexity budget: invest-in-boundaries** modular monolith: explicit ports, a shared store
  contract suite, a pure domain. One host, one database; no queues, microservices, feature flags
  or component library. §2.
- Layout: `src/CardLab.Domain` (pure, high-risk path: all money/limit code), `src/CardLab.Infrastructure`
  (EF Core, `SqlCardStore`, `HmacCardNumberLookup`, logging, `Seeder`), `src/CardLab.Api` (host),
  `web/` (SPA), `tests/` (`CardLab.Domain.Tests`, `CardLab.Integration.Tests`, `CardLab.TestKit`, `e2e/`),
  `infra/` (Bicep), `.github/workflows/` (`ci.yml`, `codeql.yml`, `deploy.yml`).

## Decisions (final)
| ID | Decision | Numbers / specifics it fixes |
|---|---|---|
| DEC-1 | README stack: C#/ASP.NET Core, React+TS, SQL Server, Azure, GitHub Actions, xUnit, Playwright, CodeQL, ZAP | free tiers only; OIDC; no stored cloud credentials |
| DEC-2 | Version lines (pin exact patches in PR-00) | .NET/ASP.NET/EF Core 10.0.x; xunit.v3 3.2.x; FsCheck.Xunit.v3 3.x; OpenTelemetry 1.x; Node 22; React 19.2 / Vite 8 / Vitest 4.1; @playwright/test 1.63; @axe-core/playwright 4.11; zaproxy/action-baseline v0.12.x; codeql-action v4 (C# `build-mode: none`); azure/login v2, webapps-deploy v3 |
| DEC-3 | One App Service F1 (Linux) for API + SPA | self-contained `linux-x64` publish; no Always On; 60 CPU-min/day quota; ≤ 60 s warm-up |
| DEC-4 | Per-card `sp_getapplock` inside the DB transaction for every card write | `@Resource='card:{id}'`, Exclusive, Transaction owner, `@LockTimeout=5000`; timeout → 503, nothing stored; spend = `SUM(Amount)` of approved rows in period, no counters |
| DEC-5 | CI DB: mssql 2022 service container on `ubuntu-latest` | `CARDLAB_TEST_CONNECTION` env; default `(localdb)\MSSQLLocalDB` |
| DEC-6 | Three .NET projects + SPA; money/limit code only under `src/CardLab.Domain` | no separate Application project |
| DEC-7 | FsCheck.Xunit.v3 properties | generators in `tests/CardLab.Domain.Tests/Generators/`; DST dates 2026-03-29, 2026-10-25 |
| DEC-8 | Card lookup = HMAC-SHA256(key, PAN digits), `binary(32)` unique | key `CardLab:LookupKey` (base64, 32 bytes); dev key committed (non-secret); `demo` key set by human in App Service |
| DEC-9 | EF Core migrations applied at startup + idempotent seed | forward-only; `Seeder.EnsureSeedAsync` recomputes lookup hashes |
| DEC-10 | Bicep: `infra/main.bicep` + `infra/demo.bicepparam` | F1 Linux plan; SQL Entra-only auth; `useFreeLimit: true`, `AutoPause`; HTTPS only |
| DEC-11 | DB auth from Azure via system-assigned managed identity | `Authentication=Active Directory Default`; roles db_ddladmin, db_datareader, db_datawriter (human one-off T-SQL) |
| DEC-12 | Structured logs via OpenTelemetry logging (console; OTLP if configured) | events `AuthorisationDecided(cardId, lastFour, category, amount, outcome, reason, durationMs)`, `ControlChanged(actorId, actorName, cardId, lastFour, action)`; `service.name=cardlab` |
| DEC-13 | UI: Minimal/Swiss light, Banking palette, Inter, CSS Modules, no component library | five hand-written primitives (button, field, table, badge, notice) |
| DEC-14 | `POST /api/authorisations` (PAN) and `POST /api/cards/{id}/simulate-purchase` (card id) share `AuthorisationService` | 200 approved/card_frozen/limit_exceeded; 404 card_not_found; 400 invalid_request (+`problems`); same body in all cases |
| DEC-15 | Periods with built-in `TimeZoneInfo` (Europe/London) + `DateOnly` + `ISOWeek` | day / week (Monday start) / month; `[localStart 00:00, localEnd 00:00)` → UTC |
| DEC-16 | Test-only `POST /api/test/reset` | mapped only when `CardLab:TestEndpoints=true` (dev + CI e2e); never on `demo`; T-I-15 asserts 404 when off |
| DEC-17 | Design system not persisted | tokens only in `web/src/styles/tokens.css`; §4.1 is the source |
| DEC-18 | Full Playwright + ZAP baseline CI-local on every PR; only `smoke` project on `demo` after deploy | ZAP `fail_action: true`; WARNs fixed or listed in `.zap/rules.tsv` with reason |
| DEC-19 | Minimal APIs with `MapGroup`, `TypedResults`, ProblemDetails, OpenAPI | endpoints thin: parse → service → map `Result` |
| DEC-20 | Vite 8 + React 19.2 + TS strict, React Router (`/`, `/cards/:id`), `ApiClient` (Http + Fake), Vitest + Testing Library, ESLint + Prettier, `@fontsource/inter` | no TanStack Query, no Next.js |
| DEC-21 | CI policy | `ticket-key` job (regex `vars.TICKET_KEY_PATTERN`, default `CL-\d+`, PR title + body); `dotnet format --verify-no-changes`, ESLint, `prettier --check`; `-warnaserror`, Nullable on; required checks `ci / backend`, `ci / web`, `ci / e2e`, `ci / ticket-key`, CodeQL |

ADRs: [ADR-001](../../kb/decisions/ADR-001-single-host-app-service-f1.md) (DEC-3, 6) ·
[ADR-002](../../kb/decisions/ADR-002-per-card-app-lock.md) (DEC-4) ·
[ADR-003](../../kb/decisions/ADR-003-keyed-card-lookup.md) (DEC-8, 14) ·
[ADR-004](../../kb/decisions/ADR-004-delivery-loop.md) (DEC-5, 9, 10, 11, 18, 21) ·
[ADR-005](../../kb/decisions/ADR-005-test-harness.md) (DEC-7, 15, 16, 20, §15).

## Interfaces (seams, §5)
Domain (`src/CardLab.Domain`, no I/O):
- `readonly record struct Money`: `Zero`, `TryFromDecimal(decimal, out Money)` (rejects > 2 dp), `Amount`, `+`, `-` (floors at zero), comparisons, `ToString() → "1234.56"`. `MoneyBounds.IsValidTransactionAmount` (£0.01..£1,000,000.00), `IsValidLimitAmount` (£0.00..£1,000,000.00).
- `enum Period { Day, Week, Month }`; `PeriodCalendar.RangeContaining(DateTimeOffset instant, Period) → PeriodRange(StartUtc, EndUtcExclusive, LocalStart)`: the London period containing an instant.
- `enum Category { Travel, Fuel, MealsAndEntertainment, SoftwareAndSubscriptions, OfficeSupplies, Advertising, ProfessionalServices, Other }`; `SpendLimit(Category, Period, Money Amount)`; `LimitWithSpend(SpendLimit, Money ApprovedSpendInPeriod)`; `LimitRemaining(Category, Period, Money Limit, Money Remaining)`.
- `enum Outcome { Approved, Declined }`; `enum Reason { Approved, CardFrozen, LimitExceeded, CardNotFound, InvalidRequest }`; `Decision(Outcome, Reason, IReadOnlyList<LimitRemaining> Limits)`.
- `AuthorisationDecider.Decide(CardStatus, IReadOnlyList<LimitWithSpend> limitsInCategory, Money amount) → Decision`: frozen → `CardFrozen`; any `spend + amount > limit` → `LimitExceeded`; else `Approved`. Remaining after decision (approved: spend + amount; declined: spend), floored at 0.
- `TransactionRules.Validate(AuthorisationRequest raw) → Result<ValidatedAuthorisation, Reason>`: `ValidatedAuthorisation(CardNumberDigits, Amount, Category, Merchant, OccurredAt)`.
- `enum CardStatus { Active, Frozen }`; `enum AuditAction { Freeze, Unfreeze, LimitSet, LimitChanged, LimitRemoved }`; `AuditEvent(CardId, Actor, AtUtc, AuditAction, OldValue?, NewValue?)`.
- `CardControls.Freeze/Unfreeze(CardStatus, CardRef, Actor, nowUtc) → ControlChange<CardStatus>?`; `SetLimit(SpendLimit? existing, SpendLimit requested, ...) → ControlChange<SpendLimit>`; `RemoveLimit(SpendLimit? existing, ...) → ControlChange<SpendLimit>?`; `ControlChange<T>(T NewState, AuditEvent Event)`; null = no-op, no audit.

Ports (`CardLab.Domain.Ports`):
- `IClock { DateTimeOffset UtcNow }` · `ICardNumberLookup { LookupHash Compute(string digits) }` · `ICurrentActor { Actor Current }`.
- `ICardStore`: `ListCardsAsync`, `GetCardAsync(CardId)`, `FindByLookupAsync(LookupHash) → CardId?`, `WithCardLockAsync<T>(CardId, Func<ICardWriter, Task<T>>)` (atomic, per-card serialised), `ListTransactionsAsync(CardId)` and `ListAuditAsync(CardId)` (newest first).
- `ICardWriter` (inside the lock): `GetStateAsync() → CardState` (status + limits), `SumApprovedSpendAsync(Category, PeriodRange)`, `AppendTransaction`, `AppendAudit`, `SetStatus`, `UpsertLimit`, `RemoveLimit(Category, Period)`; flushed on commit.

Application (`CardLab.Domain.Application`), return `Result<T, DomainError>` (`CardNotFound`, `InvalidAmount`):
- `AuthorisationService.AuthoriseAsync(AuthorisationRequest) → AuthorisationResult(Decision, TransactionId?)`; `AuthoriseByCardIdAsync(CardId, SimulatedPurchase)`: same path after card resolution. Invalid / unknown card: nothing stored. Logs one `AuthorisationDecided`.
- `CardControlsService.FreezeAsync`, `UnfreezeAsync`, `SetLimitAsync(CardId, Category, Period, decimal)`, `RemoveLimitAsync(CardId, Category, Period)`: all inside `WithCardLockAsync`; log one `ControlChanged` per real change.

Infrastructure / host / web:
- `SqlCardStore : ICardStore` (EF Core + `sp_getapplock`), `CardLabDbContext`, `Seeder.EnsureSeedAsync(IServiceProvider, SeedOptions)`; `HmacCardNumberLookup` (`IOptions<LookupOptions>`); `CardLabEvents` (LoggerMessage), `AddCardLabTelemetry(IHostApplicationBuilder)`.
- `CardLab.Api`: `MapCardEndpoints`, `MapAuthorisationEndpoints`, `MapHealthEndpoint`, `MapTestEndpoints` (conditional); security-headers middleware (HSTS, nosniff, Referrer-Policy, CSP self-only).
- `web/src/api`: `interface ApiClient` (one method per endpoint) + `HttpApiClient`, `FakeApiClient`. `web/src/format`: `formatMoney(amount: string)` → `£1,234.56`; `formatLondonTime(iso)` → `25 Oct 2026 00:30`.

## Data (§6)
Tables `BusinessAccount`, `AdminUser`, `Card` (LastFour `char(4)`, LookupHash `binary(32)` unique, Status), `SpendLimit` (unique CardId+Category+Period, `decimal(12,2)`), `Transaction` (append-only; `OccurredAt datetimeoffset(0)`, `DecidedAtUtc`, Outcome, Reason; `IX_Transaction_Spend (CardId, Category, Outcome, OccurredAt) INCLUDE (Amount)`), `AuditEvent` (append-only). Enums stored as short strings. First migration `InitialCardControls`. PAN never stored. Seed: account "Brightline Supplies Ltd" (Europe/London), admin "Sam Ellis", cards "Ops van" / "Field sales" / "Marketing" with fictional numbers `9999 0001 0000 000{1,2,3}` in `SeedOptions`.

## API (§7)
JSON under `/api`; amounts as `"50.00"` strings; ISO-8601 timestamps with offset; admin errors as ProblemDetails; no auth (actor = seeded admin). Endpoints: `GET /cards`, `GET /cards/{id}`, `POST /cards/{id}/freeze|unfreeze`, `PUT|DELETE /cards/{id}/limits/{category}/{period}`, `GET /cards/{id}/transactions|audit`, `POST /authorisations`, `POST /cards/{id}/simulate-purchase`, `GET /health` (`200 {commit, database:"ok"}` / `503 unreachable`), `POST /test/reset` (flag only). Decision body `{outcome, reason, transactionId, limits:[{category, period, limit, remaining}]}`. Wire names: snake_case categories, `day|week|month`, `active|frozen`.

## Config (§11)
`ConnectionStrings:CardLab`, `CardLab:LookupKey`, `CardLab:TestEndpoints`, `CardLab:Commit`, `CardLab:Seed:*`, optional `OTEL_EXPORTER_OTLP_ENDPOINT`; repo vars `TICKET_KEY_PATTERN`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_WEBAPP_NAME`, `DEMO_URL`. Human setup in `docs/ops/azure-setup.md` (written in the deploy PR).

## UI rules (§4.1, checkable)
Contrast ≥ 4.5:1 text, ≥ 3:1 non-text · full keyboard operation, visible 2 px focus ring · visible `<label>` on every input, inline errors with `aria-describedby` + `role="alert"` · status as text badge, never colour alone · pending state then notice on submit · amounts `£1,234.56`, times Europe/London `25 Oct 2026 00:30` · tables in `overflow-x: auto`, no page scroll at 1024-1440 px · `prefers-reduced-motion` respected · buttons ≥ 44 px tall, ≥ 8 px apart · no icons · semantic HTML, no div click handlers · no modals · components use tokens, never raw hex. Pages: `/` card table; `/cards/:id` two columns ≥ 1024 px (limits + audit | simulate + transactions).

## Testing (§15)
Boundaries: unit = pure domain + application services with fakes (no disk/network/clock/random); integration = real SQL Server via `SqlTestDatabase` (fresh DB per class) and `ApiFactory` (`WebApplicationFactory`); E2E = browser against running host, `api.reset()` before each journey except `smoke`.

Harness: `tests/CardLab.TestKit/` (Builders `CardBuilder`, `LimitBuilder`, `AuthorisationRequestBuilder`, `TransactionBuilder`, `ActorBuilder`; Fakes `InMemoryCardStore`, `FakeClock`, `FakeCardNumberLookup`, `FakeActor`, `CapturingLogger<T>`; `Contracts/CardStoreContractTests` run against both stores; `PanScanner`; `Seed`), `tests/CardLab.Domain.Tests/Generators/CardLabArbitraries.cs`, `tests/CardLab.Integration.Tests/Harness/` (`SqlTestDatabase`, `ApiFactory`, `ApiClient`, `InMemoryLogExporter`), `tests/e2e/src/` (page objects, `api.ts`, `a11y.ts`, `panScanner.ts`, `seed.ts`, Playwright projects `local` and `smoke`), `web/src/api/fakeApiClient.ts`, `web/src/test/render.tsx`. Extension recipes: §15.4.

AC → test map:
| AC | Test |
|---|---|
| AC-1, AC-3 | T-U-1/T-U-3 `Controls/FreezeTests.cs`; T-I-1 `Api/CardControlsEndpointTests.cs` |
| AC-2, AC-40 | T-U-2, T-U-20 `Controls/FreezeTests.cs` |
| AC-4, AC-5 | T-I-2 `Api/AuthorisationEndpointTests.cs` |
| AC-6..AC-8 | T-U-4..T-U-6 `Controls/LimitTests.cs`; T-I-3 `Api/LimitEndpointTests.cs` |
| AC-9 | T-U-7 `Money/MoneyBoundsTests.cs`; T-I-3 |
| AC-10..AC-12 | T-U-8..T-U-10 `Authorisation/DeciderTests.cs`; T-I-4 |
| AC-13..AC-15 | T-U-11..T-U-13 `Periods/PeriodCalendarTests.cs` |
| AC-16..AC-18 | T-U-14..T-U-16 `Authorisation/DeciderTests.cs` |
| AC-19 | T-I-5 `Api/AuthorisationEndpointTests.cs` |
| AC-20 | T-U-17 `Authorisation/TransactionRulesTests.cs`; T-I-5 |
| AC-21 | T-I-6 `Store/ConcurrencyTests.cs` (10 parallel, exactly 5 approved) |
| AC-22 | T-E-3 `simulate.spec.ts` |
| AC-23 | T-E-2 `limits.spec.ts` |
| AC-24 | T-I-7 `Privacy/NoPanTests.cs`; T-E-4 `privacy.spec.ts` |
| AC-25 | T-P-1..T-P-5 `Properties/LimitProperties.cs`, `Properties/PeriodProperties.cs` |
| AC-26..AC-29 | T-C-1..T-C-4 (CI / deploy / resource / secret evidence, validator) |
| AC-30, AC-31 | T-U-18 `Authorisation/DeciderTests.cs`; T-I-8 `Api/LimitChangeThenAuthoriseTests.cs` |
| AC-32 | T-I-9 |
| AC-33 | T-U-19 `Application/AuthorisationServiceTests.cs` |
| AC-34 | T-I-10 `Api/HealthTests.cs`; T-C-2 |
| AC-35 | T-I-14 `Performance/DecisionLatencyTests.cs` (1,000 rows, 200 requests, p95 ≤ 300 ms) |
| AC-36 | T-E-7 `smoke/j1.spec.ts` (≤ 2 s on `demo`) |
| AC-37 | T-E-5 `a11y.spec.ts` |
| AC-38 | T-W-1 `format/money.test.ts`, T-W-2 `format/time.test.ts`; T-E-6 `formatting.spec.ts` |
| AC-39 | T-I-11 `Logging/EventTests.cs` |
| (contract) | T-U-21 `Fakes/InMemoryCardStoreContractTests.cs`; T-I-12 `Store/SqlCardStoreContractTests.cs` |
| (seed) | T-I-13 `Seed/SeederTests.cs` |
| (web) | T-W-3 `pages/CardDetail.test.tsx` |
| (test endpoints) | T-I-15 `Api/TestEndpointsTests.cs` |

Commands (`docs/sdlc/project.json`): unit `dotnet test tests/CardLab.Domain.Tests && npm test --prefix web -- --run` · integration `dotnet test tests/CardLab.Integration.Tests` · e2e `npm run e2e --prefix tests/e2e` (smoke: `-- --project smoke`) · lint `dotnet format CardLab.sln --verify-no-changes && npm run lint --prefix web && npm run lint --prefix tests/e2e` · typecheck `npm run typecheck --prefix web && npm run typecheck --prefix tests/e2e` · build `dotnet build CardLab.sln -warnaserror && npm run build --prefix web`.

## Budgets
- Performance (§13): decision ≤ 300 ms p95 locally with 1,000 transactions; simulate round trip ≤ 2 s warm on `demo`; warm-up ≤ 60 s (deploy polls health up to 5 min); SPA bundle ≤ ~200 kB gzipped.
- Tests (§15.6): ~60 domain unit + 5 properties (~5 s), ~20 application unit (~2 s), ~12 web unit (~5 s), ~30 integration (~90 s), ~7 E2E specs (~3 min) + 1 smoke (~30 s). CI PR run ≤ 12 min wall clock; ZAP adds ~2 min.
- Cost: £0 (F1 + Azure SQL free offer); full E2E and ZAP never run against `demo`.

## Delivery (§14)
`ci.yml` (PR, push to `main` and `feat/**`): `ticket-key`, `backend`, `web`, `e2e` (+ ZAP). `codeql.yml`: PRs, `main`, weekly. `deploy.yml` (push to `main`, `workflow_dispatch`, env `demo`): publish → OIDC login → Bicep → zip deploy → health wait on commit → `smoke`. Rollback: redeploy previous green commit; migrations forward-only and additive.
