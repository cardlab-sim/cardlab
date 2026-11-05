import { defineConfig, devices } from '@playwright/test';

// Projects (design §15.2, DEC-18):
// - local: the full suite against a host with CardLab:TestEndpoints=true (BASE_URL, default http://localhost:5080).
//   Locally the webServer block starts the host (CARDLAB_HOST_COMMAND, default `dotnet run` with web/dist as webroot, so build the SPA first: npm run build --prefix web) unless one is already
//   listening; in CI the published host is started by the workflow first.
// - smoke: only src/smoke/** against DEMO_URL; no webServer; never calls /api/test/reset.
const baseURL = process.env.BASE_URL ?? 'http://localhost:5080';
const smokeOnly = process.argv.some((arg) => arg === 'smoke' || arg === '--project=smoke');

export default defineConfig({
  testDir: './src',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    trace: 'retain-on-failure',
  },
  ...(smokeOnly
    ? {}
    : {
        webServer: {
          command:
            process.env.CARDLAB_HOST_COMMAND ??
            'dotnet run --project ../../src/CardLab.Api -- --webroot ../../web/dist',
          url: `${baseURL}/api/health`,
          reuseExistingServer: true,
          timeout: 120_000,
        },
      }),
  projects: [
    {
      name: 'local',
      testIgnore: ['smoke/**'],
      use: { ...devices['Desktop Chrome'], baseURL },
    },
    {
      name: 'smoke',
      testMatch: ['smoke/**/*.spec.ts'],
      use: { ...devices['Desktop Chrome'], baseURL: process.env.DEMO_URL ?? baseURL },
    },
  ],
});
