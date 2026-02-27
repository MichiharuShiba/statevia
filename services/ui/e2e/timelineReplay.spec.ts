import { test, expect } from "./fixtures/test";
import { mockExecution } from "./fixtures/mockExecution";
import {
  timelineEventsPage1,
  timelineEventsPage2,
  getStateAtSeq,
} from "./fixtures/mockTimeline";

test.describe("実行履歴タイムライン・リプレイ", () => {
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

      if (url.includes("/events")) {
        const parsed = new URL(url);
        const afterSeq = parsed.searchParams.get("afterSeq");
        const after = afterSeq !== null ? parseInt(afterSeq, 10) : 0;
        const hasMore = after < 3;
        const events = after >= 3 ? timelineEventsPage2 : timelineEventsPage1;
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({ events, hasMore }),
        });
      }

      if (url.includes("/state?")) {
        const parsed = new URL(url);
        const atSeq = parsed.searchParams.get("atSeq");
        const seq = atSeq !== null ? parseInt(atSeq, 10) : 0;
        const state = getStateAtSeq(seq);
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify(state),
        });
      }

      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockExecution),
      });
    });
  });

  test("実行履歴タイムラインの見出しが表示される", async ({ executionPage }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await expect(executionPage.timelineHeading).toBeVisible();
  });

  test("タイムラインを開くとイベント一覧が表示される", async ({ executionPage }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();

    await expect(executionPage.timelineEventList.getByRole("button", { name: /#1/ })).toBeVisible();
    await expect(executionPage.timelineEventList.getByRole("button", { name: /#2/ })).toBeVisible();
    await expect(executionPage.timelineEventList.getByRole("button", { name: /#3/ })).toBeVisible();
    await expect(executionPage.timelineEventList.getByText(/GraphUpdated/).first()).toBeVisible();
    await expect(executionPage.timelineEventList.getByText(/Status →|ExecutionStatusChanged/).first()).toBeVisible();
  });

  test("時点を選択するとリプレイ表示になり「現在に戻る」が表示される", async ({ executionPage, page }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();
    await executionPage.timelineEventList.getByRole("button", { name: /#1/ }).first().click();

    await expect(page.getByRole("button", { name: "現在に戻る" }).first()).toBeVisible({ timeout: 10000 });
  });

  test("時点 seq=1 を選択するとリプレイ状態のノードが表示される", async ({ executionPage, page }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();
    await executionPage.timelineEventList.getByRole("button", { name: /#1/ }).first().click();

    await expect(page.getByRole("button", { name: "現在に戻る" }).first()).toBeVisible({ timeout: 10000 });
    await expect(executionPage.getNodeListCell("start")).toBeVisible();
    await expect(executionPage.getNodeListCell("task-a")).toBeVisible();
  });

  test("「現在に戻る」を押すとリプレイが解除され最新状態に戻る", async ({ executionPage, page }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();
    await executionPage.timelineEventList.getByRole("button", { name: /#1/ }).first().click();

    await expect(page.getByRole("button", { name: "現在に戻る" }).first()).toBeVisible({ timeout: 10000 });
    await page.getByRole("button", { name: "現在に戻る" }).first().click();

    await expect(page.getByRole("button", { name: "現在に戻る" })).toHaveCount(0);
  });

  test("リプレイ表示中は「過去の時点を表示中」バナーが表示される", async ({ executionPage, page }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();
    await executionPage.timelineEventList.getByRole("button", { name: /#3/ }).first().click();

    await expect(page.getByText(/過去の時点を表示中/)).toBeVisible({ timeout: 10000 });
  });

  test("「続きを読み込む」で2ページ目のイベントが追加される", async ({ executionPage }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();

    await expect(executionPage.timelineEventList.getByRole("button", { name: /#3/ })).toBeVisible();
    await expect(executionPage.timelineEventList.getByRole("button", { name: /#4/ })).not.toBeVisible();

    await executionPage.timelineLoadMoreButton.click();

    await expect(executionPage.timelineEventList.getByRole("button", { name: /#4/ })).toBeVisible({ timeout: 5000 });
    await expect(executionPage.timelineEventList.getByRole("button", { name: /#5/ })).toBeVisible();
  });

  test("タイムラインを再度クリックすると閉じる", async ({ executionPage }) => {
    await executionPage.goto();
    await executionPage.loadExecution();
    await executionPage.waitForExecutionLoaded();

    await executionPage.expandTimeline();
    await expect(executionPage.timelineEventList).toBeVisible();

    await executionPage.timelineHeading.click();
    await expect(executionPage.timelineBody).toBeHidden();
  });
});
