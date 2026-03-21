import { test, expect } from "./fixtures/test";
import { mockExecution } from "./fixtures/mockExecution";

test.describe("execution", () => {
  test.beforeEach(async ({ page }) => {
    await page.route("**/api/core/workflows/**", (route) => {
      const url = route.request().url();
      if (url.includes("/stream")) {
        return route.fulfill({
          status: 200,
          contentType: "text/event-stream",
          body: "",
        });
      }
      if (url.includes("/events")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            events: [
              { seq: 1, type: "GraphUpdated", executionId: "ex-1", patch: { nodes: [] }, at: new Date().toISOString() },
              { seq: 2, type: "ExecutionStatusChanged", executionId: "ex-1", to: "ACTIVE", at: new Date().toISOString() },
            ],
            hasMore: false,
          }),
        });
      }
      if (url.includes("/state?")) {
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(mockExecution),
        });
      }
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockExecution),
      });
    });
  });

  test("Loadを押すと実行情報が表示される", async ({ executionPage }) => {
    await executionPage.goto();

    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded({ graphId: "hello" });

    await expect(executionPage.executionIdInput).toHaveValue("ex-1");
  });

  test("リスト表示でノード一覧が表示される", async ({ executionPage }) => {
    await executionPage.goto();

    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await expect(executionPage.getNodeListCell("start")).toBeVisible();
    await expect(executionPage.getNodeListCell("task-a")).toBeVisible();
  });

  test("グラフ表示に切り替えられる", async ({ executionPage }) => {
    await executionPage.goto();

    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();
    await executionPage.switchToGraphView();

    await executionPage.waitForGraphView();
  });

  test("実行履歴タイムラインが表示される", async ({ executionPage, page }) => {
    await executionPage.goto();

    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await expect(page.getByRole("heading", { name: "実行履歴タイムライン" })).toBeVisible();
  });

  test("タイムラインで時点を選ぶと「現在に戻る」が表示され、押すとリプレイが解除される", async ({ executionPage, page }) => {
    await executionPage.goto();

    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();

    const firstEventButton = executionPage.timelineEventList.getByRole("button", { name: /#1/ }).first();
    await firstEventButton.click();

    await expect(page.getByRole("button", { name: "現在に戻る" }).first()).toBeVisible({ timeout: 10000 });

    await page.getByRole("button", { name: "現在に戻る" }).first().click();

    await expect(page.getByRole("button", { name: "現在に戻る" })).toHaveCount(0);
  });
});
