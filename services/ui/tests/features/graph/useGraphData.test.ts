import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import { useGraphData } from "../../../app/features/graph/useGraphData";
import type { ExecutionNodeDTO, WorkflowView } from "../../../app/lib/types";
import { getGraphDefinition } from "../../../app/graphs/registry";

function execution(nodes: ExecutionNodeDTO[], graphId = "hello"): WorkflowView {
  return {
    displayId: "ex-1",
    resourceId: "res-1",
    status: "Running",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    graphId,
    nodes
  };
}

describe("useGraphData", () => {
  it("execution が null のとき null を返す", () => {
    // Arrange
    const def = getGraphDefinition("hello");

    // Act
    const { result } = renderHook(() => useGraphData(null, def));

    // Assert
    expect(result.current).toBeNull();
  });

  it("execution と definition があるとき GraphData を返す", () => {
    // Arrange
    const exec = execution([], "hello");
    const def = getGraphDefinition("hello");

    // Act
    const { result } = renderHook(() => useGraphData(exec, def));

    // Assert
    expect(result.current).not.toBeNull();
    expect(result.current?.graphId).toBe("hello");
    expect(result.current?.definitionBased).toBe(true);
    expect(result.current?.mergedNodes.length).toBeGreaterThan(0);
    expect(result.current?.nodes.length).toBe(result.current?.mergedNodes.length);
    expect(result.current?.edges.length).toBeGreaterThan(0);
    expect(result.current?.groups).toBeDefined();
  });

  it("definition が null のとき definitionBased は false", () => {
    // Arrange
    const exec = execution([
      { nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }
    ]);

    // Act
    const { result } = renderHook(() => useGraphData(exec, null));

    // Assert
    expect(result.current).not.toBeNull();
    expect(result.current?.definitionBased).toBe(false);
    expect(result.current?.mergedNodes).toHaveLength(1);
    expect(result.current?.edges).toHaveLength(0);
  });
});
