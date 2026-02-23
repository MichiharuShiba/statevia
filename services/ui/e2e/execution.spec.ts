import { test, expect } from "./fixtures/test";
import { mockExecution } from "./fixtures/mockExecution";

test.describe("execution", () => {
  test.beforeEach(async ({ page }) => {
    await page.route("**/api/core/executions/**", (route) => {
      const url = route.request().url();
      if (url.includes("/stream")) {
        return route.fulfill({
          status: 200,
          contentType: "text/event-stream",
          body: "",
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
});
