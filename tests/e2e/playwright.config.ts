import { defineConfig, devices } from '@playwright/test';

// Projects (design §15.2, DEC-18):
// - local: the full suite against a host started from the published output with CardLab:TestEndpoints=true
//   (BASE_URL, default http://localhost:5080). PR-10 adds the webServer block (command from CARDLAB_HOST_COMMAND,
//   reuseExistingServer locally) and the CI settings (retries, reporter).
// - smoke: only src/smoke/** against DEMO_URL; never calls /api/test/reset.
// Skeleton (PR-00): projects defined so `--project smoke` resolves; owned by PR-10.
const baseURL = process.env.BASE_URL ?? 'http://localhost:5080';

export default defineConfig({
  testDir: './src',
  fullyParallel: false,
  workers: 1,
  use: {
    trace: 'retain-on-failure',
  },
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
