import { describe, expect, it } from "vitest";
import { isPublishEventAvailable, resolveWaitResumeEvents } from "../../../features/executions/lib/waitResumeEvents";

describe("resolveWaitResumeEvents", () => {
  it("allowedEvents を優先して返す", () => {
    // Arrange
    const node = { allowedEvents: ["approve", "reject"], waitKey: "legacy" };

    // Act
    const result = resolveWaitResumeEvents(node);

    // Assert
    expect(result).toEqual(["approve", "reject"]);
  });

  it("allowedEvents が空なら waitKey を単一要素で返す", () => {
    // Arrange
    const node = { allowedEvents: [], waitKey: "resume" };

    // Act
    const result = resolveWaitResumeEvents(node);

    // Assert
    expect(result).toEqual(["resume"]);
  });

  it("空白のみのイベント名を除外し重複を除去する", () => {
    // Arrange
    const node = { allowedEvents: [" approve ", "", "Approve", "reject"] };

    // Act
    const result = resolveWaitResumeEvents(node);

    // Assert
    expect(result).toEqual(["approve", "reject"]);
  });

  it("イベントが無ければ空配列を返す", () => {
    // Arrange
    const node = { allowedEvents: null, waitKey: null };

    // Act
    const result = resolveWaitResumeEvents(node);

    // Assert
    expect(result).toEqual([]);
  });
});

describe("isPublishEventAvailable", () => {
  it("WAITING が 1 件かつ許可イベントが 1 件なら true", () => {
    // Arrange
    const execution = {
      status: "Running",
      cancelRequested: false,
      nodes: [{ status: "WAITING", waitKey: "approve", allowedEvents: null }]
    };

    // Act / Assert
    expect(isPublishEventAvailable(execution)).toBe(true);
  });

  it("許可イベントが複数なら false", () => {
    // Arrange
    const execution = {
      status: "Running",
      nodes: [{ status: "WAITING", allowedEvents: ["approve", "reject"] }]
    };

    // Act / Assert
    expect(isPublishEventAvailable(execution)).toBe(false);
  });

  it("WAITING が複数なら false", () => {
    // Arrange
    const execution = {
      status: "Running",
      nodes: [
        { status: "WAITING", waitKey: "a" },
        { status: "WAITING", waitKey: "b" }
      ]
    };

    // Act / Assert
    expect(isPublishEventAvailable(execution)).toBe(false);
  });

  it("終端実行なら false", () => {
    // Arrange
    const execution = {
      status: "Completed",
      nodes: [{ status: "WAITING", waitKey: "approve" }]
    };

    // Act / Assert
    expect(isPublishEventAvailable(execution)).toBe(false);
  });
});
