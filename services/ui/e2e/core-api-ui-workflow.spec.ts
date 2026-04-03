import { test, expect } from "@playwright/test";
import { ExecutionPage } from "./pages/ExecutionPage";
import { e2eWaitWorkflowYaml } from "./fixtures/e2eWaitWorkflowYaml";

/**
 * Core-API 実体 + Next プロキシ経由の UI E2E（STV-401/402 の UI 側）。
 *
 * 事前: Core-API を起動し、マイグレーション済み DB を用意する。
 * 実行例:
 *   CORE_API_E2E_URL=http://localhost:8080 npx playwright test e2e/core-api-ui-workflow.spec.ts
 *
 * `playwright.config` は CORE_API_E2E_URL 設定時に CORE_API_INTERNAL_BASE を渡す。
 */
const apiBase = process.env.CORE_API_E2E_URL?.replace(/\/$/, "") ?? "";
const tenantHeaders = { "X-Tenant-Id": "default", "Content-Type": "application/json" };

test.describe("Core API + UI (real)", () => {
  test.describe.configure({ mode: "serial", timeout: 120_000 });
  test.skip(!apiBase, "CORE_API_E2E_URL が未設定のためスキップ");

  test("STV-401: Load → Cancel で Cancelled かつ成功トースト", async ({ page, request }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const defName = `e2e-ui-wait-${suffix}`;
    const yaml = e2eWaitWorkflowYaml(defName);

    const createDef = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defName, yaml }
    });
    expect(createDef.ok(), await createDef.text()).toBeTruthy();
    const defJson = (await createDef.json()) as { displayId: string };

    const start = await request.post(`${apiBase}/v1/workflows`, {
      headers: tenantHeaders,
      data: { definitionId: defJson.displayId, input: {} }
    });
    expect(start.ok(), await start.text()).toBeTruthy();
    const wf = (await start.json()) as { displayId: string };

    const executionPage = new ExecutionPage(page);
    await executionPage.goto();
    await executionPage.expectHeaderVisible();
    await executionPage.executionIdInput.fill(wf.displayId);
    await executionPage.loadExecution();
    await executionPage.waitForExecutionStatus("Running");

    await executionPage.cancelButton.click();
    // status ロールのアクセシブルネームは子テキストに依存しないことがあるため、name + フォールバックの両方に耐える。
    const successToast = page
      .getByRole("status", { name: "CancelExecution accepted" })
      .or(page.getByRole("status").filter({ hasText: "CancelExecution accepted" }));
    await expect(successToast).toBeVisible({ timeout: 60_000 });
    await executionPage.waitForExecutionStatus("Cancelled", { timeout: 30_000 });
  });

  test("STV-402: Cancel が 409 のときトーストに 409 が含まれる", async ({ page, request }) => {
    const suffix = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const defName = `e2e-ui-409-${suffix}`;
    const yaml = e2eWaitWorkflowYaml(defName);

    const createDef = await request.post(`${apiBase}/v1/definitions`, {
      headers: tenantHeaders,
      data: { name: defName, yaml }
    });
    expect(createDef.ok()).toBeTruthy();
    const defJson = (await createDef.json()) as { displayId: string };

    const start = await request.post(`${apiBase}/v1/workflows`, {
      headers: tenantHeaders,
      data: { definitionId: defJson.displayId, input: {} }
    });
    expect(start.ok()).toBeTruthy();
    const wf = (await start.json()) as { displayId: string };

    await page.route("**/api/core/workflows/*/cancel", async (route) => {
      await route.fulfill({
        status: 409,
        contentType: "application/json",
        body: JSON.stringify({
          error: { code: "IDEMPOTENCY_KEY_CONFLICT", message: "conflict for e2e" }
        })
      });
    });

    const executionPage = new ExecutionPage(page);
    await executionPage.goto();
    await executionPage.expectHeaderVisible();
    await executionPage.executionIdInput.fill(wf.displayId);
    await executionPage.loadExecution();
    await executionPage.waitForExecutionStatus("Running");

    await executionPage.cancelButton.click();
    await expect(page.getByText(/409/)).toBeVisible({ timeout: 15_000 });
  });
});
