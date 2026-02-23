import { describe, expect, it } from "vitest";
import { getResumeDisabledReason } from "../../../app/features/nodes/useNodeCommands";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../../app/lib/types";

function execution(overrides: Partial<ExecutionDTO> = {}): ExecutionDTO {
  return {
    executionId: "ex-1",
    status: "ACTIVE",
    graphId: "g-1",
    cancelRequestedAt: null,
    canceledAt: null,
    failedAt: null,
    completedAt: null,
    nodes: [],
    ...overrides
  };
}

function node(overrides: Partial<ExecutionNodeDTO> = {}): ExecutionNodeDTO {
  return {
    nodeId: "n-1",
    nodeType: "TASK",
    status: "WAITING",
    attempt: 1,
    workerId: null,
    waitKey: "wk-1",
    canceledByExecution: false,
    ...overrides
  };
}

describe("getResumeDisabledReason", () => {
  it("execution と node が有効で node が WAITING のとき null を返す", () => {
    // Arrange
    const exec = execution();
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBeNull();
  });

  it("execution が null のときメッセージを返す", () => {
    // Arrange
    const n = node();

    // Act
    const result = getResumeDisabledReason(null, n);

    // Assert
    expect(result).toBe("Execution が未読込です");
  });

  it("node が null のときメッセージを返す", () => {
    // Arrange
    const exec = execution();

    // Act
    const result = getResumeDisabledReason(exec, null);

    // Assert
    expect(result).toBe("Node を選択してください");
  });

  it("execution が終了状態 (COMPLETED) のときメッセージを返す", () => {
    // Arrange
    const exec = execution({ status: "COMPLETED" });
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("Executionは終了しています");
  });

  it("execution が終了状態 (FAILED) のときメッセージを返す", () => {
    // Arrange
    const exec = execution({ status: "FAILED" });
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("Executionは終了しています");
  });

  it("cancel が要求済みのときメッセージを返す", () => {
    // Arrange
    const exec = execution({ cancelRequestedAt: "2026-01-01T00:00:00Z" });
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("Cancel要求済みのため、Resumeなど進行系操作はできません");
  });

  it("node が WAITING でないときメッセージを返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ status: "RUNNING" });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("WAITING 状態のノードのみ Resume できます");
  });

  it("execution が終了状態 (CANCELED) のときメッセージを返す", () => {
    // Arrange
    const exec = execution({ status: "CANCELED" });
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("Executionは終了しています");
  });

  it("node status が IDLE のときメッセージを返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ status: "IDLE" });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("WAITING 状態のノードのみ Resume できます");
  });

  it("node status が SUCCEEDED のときメッセージを返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ status: "SUCCEEDED" });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBe("WAITING 状態のノードのみ Resume できます");
  });
});

describe("getResumeDisabledReason (境界値)", () => {
  it("execution.nodes が空配列でも ACTIVE なら null を返す（ノードは別途渡す）", () => {
    // Arrange
    const exec = execution({ nodes: [] });
    const n = node();

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBeNull();
  });

  it("attempt 0 で WAITING なら null を返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ attempt: 0 });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBeNull();
  });

  it("waitKey が null で WAITING なら null を返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ waitKey: null });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBeNull();
  });

  it("waitKey が空文字で WAITING なら null を返す", () => {
    // Arrange
    const exec = execution();
    const n = node({ waitKey: "" });

    // Act
    const result = getResumeDisabledReason(exec, n);

    // Assert
    expect(result).toBeNull();
  });
});
