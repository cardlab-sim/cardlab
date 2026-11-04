# ADR-001: One ASP.NET Core host (API + SPA) on Azure App Service F1 (Linux)

- **Status:** accepted (feature 001-card-controls, design DEC-3, DEC-6, 2026-11-04)
- **Context:** CardLab must run in Azure at near £0/month with nothing that bills by the hour,
  deployed by GitHub Actions through OIDC. The product is a long-lived practice platform, so
  boundaries matter, but traffic is tests and demos.
- **Decision:** a modular monolith: `CardLab.Domain` (pure), `CardLab.Infrastructure` (EF Core,
  SQL Server, logging), `CardLab.Api` (Minimal API host that also serves the Vite-built React SPA
  from `wwwroot/`), deployed as one self-contained `linux-x64` zip to one App Service plan F1 with
  a system-assigned managed identity, described in `infra/main.bicep`.
- **Alternatives:** Azure Container Apps consumption (free grant, but bills per second beyond it,
  needs an image pipeline); Static Web Apps + App Service (two resources, CORS); a single .NET
  project (no dependency firewall for the money path).
- **Consequences:** no Always On (cold start after idle, within the 60 s warm-up NFR); a daily
  60 CPU-minute quota, so heavy test suites run CI-local and only one smoke journey hits the
  deployed site; no independent scaling; money/limit code lives only under `src/CardLab.Domain/`,
  which a later path-based risk-tier map can mark highest risk.
