import { defineConfig, devices } from "@playwright/test";

const coreApiE2E = process.env.CORE_API_E2E_URL;
const coreApiInternalBase = process.env.CORE_API_INTERNAL_BASE ?? coreApiE2E;
/** Playwright 起動の Next が route ハンドラで落ちないよう、未設定時はダミー URL を渡す（mock E2E はブラウザ側 route で吸収）。 */
const coreApiInternalForWebServer =
  (coreApiE2E && coreApiInternalBase ? coreApiInternalBase : process.env.CORE_API_INTERNAL_BASE) ??
  "http://localhost:8080";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : process.env.CORE_API_E2E_URL ? 2 : undefined,
  reporter: "html",
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: "npm run dev",
    url: "http://localhost:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
    env: {
      ...process.env,
      CORE_API_INTERNAL_BASE: coreApiInternalForWebServer.replace(/\/$/, ""),
      // 実 API E2E は request 側で X-Tenant-Id: default を付ける。ブラウザはヘッダ無しのことが多いのでプロキシが既定テナントを転送する。
      ...(coreApiE2E ? { CORE_API_TENANT_ID: process.env.CORE_API_TENANT_ID ?? "default" } : {}),
    },
  },
});
