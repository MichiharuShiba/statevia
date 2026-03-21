import { test, expect } from "@playwright/test";

/**
 * Core-API を直接叩くオプション E2E（Phase 4.1/4.2 の補助）。
 * 実行例: CORE_API_E2E_URL=http://localhost:8080 npx playwright test e2e/core-api-real.spec.ts
 */
const apiBase = process.env.CORE_API_E2E_URL?.replace(/\/$/, "") ?? "";

test.describe("Core API (direct)", () => {
  test.skip(!apiBase, "CORE_API_E2E_URL が未設定のためスキップ（例: http://localhost:8080）");

  test("GET /v1/health は ok を返す", async ({ request }) => {
    const res = await request.get(`${apiBase}/v1/health`);
    expect(res.ok()).toBeTruthy();
    const json = (await res.json()) as { status?: string };
    expect(json.status).toBe("ok");
  });

  test("GET /v1/workflows は 200（テナント default）", async ({ request }) => {
    const res = await request.get(`${apiBase}/v1/workflows`, {
      headers: { "X-Tenant-Id": "default" }
    });
    expect(res.status()).toBe(200);
  });
});
