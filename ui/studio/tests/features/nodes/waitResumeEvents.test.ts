import { describe, expect, it } from "vitest";
import { resolveWaitResumeEvents } from "../../../features/executions/lib/waitResumeEvents";

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
