# ADR-004: Delivery loop: CI-local verification on every PR, OIDC + Bicep deploy from main

- **Status:** accepted (feature 001-card-controls, design DEC-5, DEC-9, DEC-10, DEC-11, DEC-18,
  DEC-21, 2026-11-04)
- **Context:** Every PR must run build, unit/integration tests, E2E, static and dynamic security
  scans and a ticket-key check; a green `main` deploys automatically; no secrets in the repo; the
  free hosting tier has a daily CPU quota.
- **Decision:** `ci.yml` (PRs, `main`, `feat/**`): `ticket-key` (regex from
  `vars.TICKET_KEY_PATTERN`, default `CL-\d+`), `backend` (ubuntu + SQL Server 2022 service
  container: build with warnings as errors, unit, integration), `web` (lint, typecheck, Vitest,
  build), `e2e` (published host + SPA started locally: Playwright with axe and keyboard journeys,
  then ZAP baseline failing on FAIL-level alerts with a reviewed `.zap/rules.tsv`). `codeql.yml`
  for C# (`build-mode: none`) and JavaScript/TypeScript. `deploy.yml` (push to `main`,
  `workflow_dispatch`): `azure/login` via OIDC, `az deployment group create` with
  `infra/main.bicep`, zip deploy, wait for `/api/health` to report the new commit, then one
  Playwright smoke journey against the deployed URL. Schema migrations and idempotent seeding run
  at application startup; the web app reaches Azure SQL with its managed identity.
- **Alternatives:** LocalDB on Windows runners; Testcontainers; running the full suite against the
  deployed site (quota, pre-merge impossible); migrations from the runner (needs SQL network
  access and credentials); SQL authentication with a stored password.
- **Consequences:** human one-off setup (resource group, federated credential, repo variables,
  SQL user for the identity, lookup key, branch protection) documented in `docs/ops/azure-setup.md`;
  rollback is re-running the deploy on the previous green commit; schema is forward-only.
