import { test, expect } from "./fixtures/test";

test.describe("smoke", () => {
  test("トップページが表示され、ヘッダーとExecution ID入力・Loadボタンがある", async ({ executionPage }) => {
    await executionPage.goto();

    await executionPage.expectHeaderVisible();
  });

  test("Execution IDの初期値がex-1である", async ({ executionPage }) => {
    await executionPage.goto();

    await expect(executionPage.executionIdInput).toHaveValue("ex-1");
  });
});
