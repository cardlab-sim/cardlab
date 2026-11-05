# CardLab

CardLab is a fictional business credit card platform, built as a practice ground for agentic software delivery and quality engineering. All names and data are fictional.

- **Stack:** C# / ASP.NET Core, React + TypeScript, SQL Server, Azure, GitHub Actions.
- **Delivery:** features are built with Claude Code through a staged lifecycle (requirements → design → plan → build → review → validation), with human gates between stages.
- **Quality platform:** risk-tiered review, event-driven triage agents, PR analytics traced to tickets and incidents, and per-release audit evidence.

Nothing here is production software.

## Run locally

Prerequisites: .NET 10 SDK, Node 22 and SQL Server LocalDB (instance `MSSQLLocalDB`, the default connection). Set `CARDLAB_TEST_CONNECTION` to run the integration tests against another SQL Server; the host (and so the end-to-end run) uses `ConnectionStrings__CardLab`.

```bash
npm ci --prefix web && npm run build --prefix web    # build the SPA first
dotnet run --project src/CardLab.Api -- --webroot ../../web/dist  # UI http://localhost:5080/ · health /api/health
```

Tests (from the repository root):

```bash
dotnet test tests/CardLab.Domain.Tests               # unit (domain)
npm test --prefix web -- --run                       # unit (web)
dotnet test tests/CardLab.Integration.Tests          # integration
npm ci --prefix tests/e2e && npx --prefix tests/e2e playwright install chromium firefox
npm run e2e --prefix tests/e2e -- --project local --project local-firefox
```

The e2e run starts the host itself unless one is already listening on port 5080, in which case it reuses that host.

Azure setup: see [docs/ops/azure-setup.md](docs/ops/azure-setup.md).
