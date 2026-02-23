import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useNodeCommands } from "../../../app/features/nodes/useNodeCommands";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../../app/lib/types";
import * as api from "../../../app/lib/api";

const execWithNodes = (nodes: ExecutionNodeDTO[]): ExecutionDTO => ({
  executionId: "ex-1",
  status: "ACTIVE",
  graphId: "g-1",
  cancelRequestedAt: null,
  canceledAt: null,
  failedAt: null,
  completedAt: null,
  nodes
});

describe("useNodeCommands", () => {
  beforeEach(() => {
    vi.spyOn(api, "apiPost").mockResolvedValue({
      executionId: "ex-1",
      command: "resume",
      accepted: true,
      idempotencyKey: "key-1"
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("resumeNode と loading を返す", () => {
    // Arrange
    const exec = execWithNodes([
      { nodeId: "n-1", nodeType: "TASK", status: "WAITING", attempt: 1, workerId: null, waitKey: "wk-1", canceledByExecution: false }
    ]);

    // Act
    const { result } = renderHook(() => useNodeCommands(exec));

    // Assert
    expect(result.current.loading).toBe(false);
    expect(typeof result.current.resumeNode).toBe("function");
  });

  it("resumeNode 成功時に apiPost と onSuccess を呼ぶ", async () => {
    // Arrange
    const exec = execWithNodes([
      { nodeId: "n-1", nodeType: "TASK", status: "WAITING", attempt: 1, workerId: null, waitKey: "wk-1", canceledByExecution: false }
    ]);
    const onSuccess = vi.fn();
    const { result } = renderHook(() => useNodeCommands(exec, { onSuccess }));

    // Act
    await act(async () => {
      result.current.resumeNode("n-1");
    });

    // Assert
    expect(api.apiPost).toHaveBeenCalledWith(
      "/executions/ex-1/nodes/n-1/resume",
      { resumeKey: "wk-1" }
    );
    expect(onSuccess).toHaveBeenCalled();
  });

  it("apiPost が throw したとき onError を呼ぶ", async () => {
    // Arrange
    vi.mocked(api.apiPost).mockRejectedValueOnce(new Error("Network error"));
    const exec = execWithNodes([
      { nodeId: "n-1", nodeType: "TASK", status: "WAITING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
    ]);
    const onError = vi.fn();
    const { result } = renderHook(() => useNodeCommands(exec, { onError }));

    // Act
    await act(async () => {
      result.current.resumeNode("n-1");
    });

    // Assert
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  it("execution が null のとき apiPost を呼ばない", async () => {
    // Arrange
    const { result } = renderHook(() => useNodeCommands(null));

    // Act
    await act(async () => {
      result.current.resumeNode("n-1");
    });

    // Assert
    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("nodeId が execution に無いとき apiPost を呼ばない", async () => {
    // Arrange
    const exec = execWithNodes([
      { nodeId: "n-1", nodeType: "TASK", status: "WAITING", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false }
    ]);
    const { result } = renderHook(() => useNodeCommands(exec));

    // Act
    await act(async () => {
      result.current.resumeNode("n-missing");
    });

    // Assert
    expect(api.apiPost).not.toHaveBeenCalled();
  });
});
