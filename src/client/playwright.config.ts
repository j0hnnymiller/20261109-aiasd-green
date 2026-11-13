import { defineConfig, devices } from '@playwright/test'

const connectionString = process.env.TASK_MANAGER_TEST_CONNECTION_STRING
if (!connectionString) {
  throw new Error('TASK_MANAGER_TEST_CONNECTION_STRING must point to an isolated disposable PostgreSQL database.')
}

const apiUrl = 'http://127.0.0.1:5080'
const appUrl = 'http://localhost:5173'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: appUrl,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --no-build --no-launch-profile --project ../TaskManager.Api/TaskManager.Api.csproj --urls http://127.0.0.1:5080',
      url: `${apiUrl}/api/workspace`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Testing',
        ConnectionStrings__TaskManager: connectionString,
        Security__AllowedOrigin: appUrl,
        AbuseControls__Provisioning__PermitLimit: '50',
        AbuseControls__Provisioning__WindowSeconds: '60',
        AbuseControls__ProtectedRequests__PermitLimit: '100',
        AbuseControls__ProtectedRequests__WindowSeconds: '60',
      },
    },
    {
      command: 'npm run dev -- --host localhost --port 5173',
      url: appUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        API_PROXY_TARGET: apiUrl,
      },
    },
  ],
})
