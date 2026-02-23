import { describe, expect, it } from "vitest";
import { getNodeWithFallback, type GraphData } from "../../../app/features/graph/useGraphData";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../../app/lib/types";

function execution(nodes: ExecutionNodeDTO[]): ExecutionDTO {
  return {
    executionId: "ex-1",
    status: "ACTIVE",
    graphId: "g-1",
    cancelRequestedAt: null,
    canceledAt: null,
    failedAt: null,
    completedAt: null,
    nodes
  };
}

function graphData(mergedNodes: GraphData["mergedNodes"]): GraphData {
  return {
    graphId: "g-1",
    definitionBased: true,
    mergedNodes,
    nodes: [],
    edges: [],
    groups: []
  };
}

describe("getNodeWithFallback", () => {
  it("execution が null のとき null を返す", () => {
    // Arrange
    const graph = graphData([{ nodeId: "n-1", nodeType: "TASK", label: "n-1", status: "IDLE", attempt: 0, waitKey: null, canceledByExecution: false }]);

    // Act
    const result = getNodeWithFallback(null, graph, "n-1");

    // Assert
    expect(result).toBeNull();
  });

  it("nodeId が null のとき null を返す", () => {
    // Arrange
    const exec = execution([{ nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }]);

    // Act
    const result = getNodeWithFallback(exec, null, null);

    // Assert
    expect(result).toBeNull();
  });

  it("execution に存在するときランタイムノードを返す", () => {
    // Arrange
    const runtimeNode: ExecutionNodeDTO = {
      nodeId: "n-1",
      nodeType: "TASK",
      status: "RUNNING",
      attempt: 2,
      workerId: "w-1",
      waitKey: null,
      canceledByExecution: false
    };
    const exec = execution([runtimeNode]);

    // Act
    const result = getNodeWithFallback(exec, null, "n-1");

    // Assert
    expect(result).toEqual(runtimeNode);
    expect(result?.workerId).toBe("w-1");
  });

  it("execution に無く graphData にあるときマージノードを返す", () => {
    // Arrange
    const exec = execution([]);
    const mergedNodes = [
      { nodeId: "n-2", nodeType: "WAIT", label: "Wait", status: "IDLE" as const, attempt: 0, waitKey: null, canceledByExecution: false }
    ];
    const graph = graphData(mergedNodes);

    // Act
    const result = getNodeWithFallback(exec, graph, "n-2");

    // Assert
    expect(result).not.toBeNull();
    expect(result?.nodeId).toBe("n-2");
    expect(result?.nodeType).toBe("WAIT");
    expect(result?.status).toBe("IDLE");
    expect(result?.workerId).toBeNull();
  });

  it("nodeId が execution にも graphData にも無いとき null を返す", () => {
    // Arrange
    const exec = execution([{ nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }]);
    const graph = graphData([]);

    // Act
    const result = getNodeWithFallback(exec, graph, "n-unknown");

    // Assert
    expect(result).toBeNull();
  });

  it("両方にノードがあるときランタイムノードを優先する", () => {
    // Arrange
    const runtimeNode: ExecutionNodeDTO = { nodeId: "n-1", nodeType: "TASK", status: "RUNNING", attempt: 1, workerId: "w-1", waitKey: null, canceledByExecution: false };
    const exec = execution([runtimeNode]);
    const graph = graphData([
      { nodeId: "n-1", nodeType: "TASK", label: "n-1", status: "IDLE", attempt: 0, waitKey: null, canceledByExecution: false }
    ]);

    // Act
    const result = getNodeWithFallback(exec, graph, "n-1");

    // Assert
    expect(result?.status).toBe("RUNNING");
    expect(result?.workerId).toBe("w-1");
  });
});

describe("getNodeWithFallback (境界値)", () => {
  it("nodeId が空文字のときは null を返す（!nodeId で弾かれる）", () => {
    // Arrange
    const exec = execution([{ nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }]);
    const graph = graphData([]);

    // Act
    const result = getNodeWithFallback(exec, graph, "");

    // Assert
    expect(result).toBeNull();
  });

  it("execution.nodes が空・graphData.mergedNodes にのみ存在するときマージノードを返す", () => {
    // Arrange
    const exec = execution([]);
    const graph = graphData([
      { nodeId: "only-in-merged", nodeType: "TASK", label: "Merged", status: "IDLE" as const, attempt: 0, waitKey: null, canceledByExecution: false }
    ]);

    // Act
    const result = getNodeWithFallback(exec, graph, "only-in-merged");

    // Assert
    expect(result?.nodeId).toBe("only-in-merged");
    expect(result?.workerId).toBeNull();
  });

  it("graphData が null で execution にノードが無いときは null", () => {
    // Arrange
    const exec = execution([]);

    // Act
    const result = getNodeWithFallback(exec, null, "n-1");

    // Assert
    expect(result).toBeNull();
  });
});
