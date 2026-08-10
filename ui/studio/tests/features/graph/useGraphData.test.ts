import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import { getNodeWithFallback, useGraphData } from "../../../features/executions/hooks/useGraphData";
import type { ExecutionNodeDTO, ExecutionView } from "@/features/executions/types";
import { getGraphDefinition } from "@/features/executions/graphs/registry";
import type { GraphDefinition } from "@/features/executions/graphs/types";

function execution(nodes: ExecutionNodeDTO[], graphId = "hello"): ExecutionView {
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

describe("getNodeWithFallback", () => {
  it("定義名で選択したとき同名の完了 Wait より WAITING を返す", () => {
    // Arrange
    const def: GraphDefinition = {
      graphId: "cyclic-wait",
      nodes: [{ nodeName: "cycle.decide", nodeType: "Wait" }],
      edges: []
    };
    const exec = execution(
      [
        {
          nodeId: "decide-old",
          nodeName: "cycle.decide",
          nodeType: "Wait",
          status: "SUCCEEDED",
          attempt: 1,
          workerId: null,
          waitKey: null,
          allowedEvents: ["Again", "Finish"],
          canceledByExecution: false
        },
        {
          nodeId: "decide-new",
          nodeName: "cycle.decide",
          nodeType: "Wait",
          status: "WAITING",
          attempt: 2,
          workerId: null,
          waitKey: null,
          allowedEvents: ["Again", "Finish"],
          canceledByExecution: false
        }
      ],
      "cyclic-wait"
    );
    const { result } = renderHook(() => useGraphData(exec, def));

    // Act
    const resolved = getNodeWithFallback(exec, result.current, "cycle.decide");

    // Assert
    expect(resolved?.nodeId).toBe("decide-new");
    expect(resolved?.status).toBe("WAITING");
  });
});
