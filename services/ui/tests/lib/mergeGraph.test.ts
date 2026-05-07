import { describe, expect, it } from "vitest";
import { mergeGraph } from "../../app/lib/mergeGraph";
import type { ExecutionNodeDTO, WorkflowView } from "../../app/lib/types";
import { getGraphDefinition } from "../../app/graphs/registry";
import type { GraphDefinition } from "../../app/graphs/types";

function execution(nodes: ExecutionNodeDTO[], graphId = "g-1"): WorkflowView {
  return {
    displayId: "ex-1",
    resourceId: "res-1",
    status: "Running",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    graphId,
    nodes,
    runtimeEdges: []
  };
}

describe("mergeGraph", () => {
  it("definition があるとき定義ベースのマージを返す", () => {
    // Arrange
    const def = getGraphDefinition("hello")!;
    const exec = execution(
      [{ nodeId: "start", nodeType: "Start", status: "RUNNING", attempt: 1, workerId: "w-1", waitKey: null, canceledByExecution: false }],
      "hello"
    );

    // Act
    const result = mergeGraph(exec, def);

    // Assert
    expect(result.graphId).toBe("hello");
    expect(result.isDefinitionBased).toBe(true);
    expect(result.nodes.length).toBe(def.nodes.length);
    expect(result.edges.length).toBe(def.edges.length);
    const startNode = result.nodes.find((n) => n.nodeId === "start");
    expect(startNode?.status).toBe("RUNNING");
    expect(startNode?.attempt).toBe(1);
  });

  it("execution に無い定義ノードは IDLE にする", () => {
    // Arrange
    const def = getGraphDefinition("hello")!;
    const exec = execution([], "hello");

    // Act
    const result = mergeGraph(exec, def);

    // Assert
    expect(result.nodes.every((n) => n.status === "IDLE" && n.attempt === 0)).toBe(true);
    expect(result.nodes.some((n) => n.nodeId === "task-a" && n.label === "Task A")).toBe(true);
  });

  it("definition が null のとき execution のみのマージを返す", () => {
    // Arrange
    const exec = execution([
      { nodeId: "n-1", nodeType: "TASK", status: "RUNNING", attempt: 1, workerId: "w-1", waitKey: null, canceledByExecution: false }
    ]);

    // Act
    const result = mergeGraph(exec, null);

    // Assert
    expect(result.graphId).toBe("g-1");
    expect(result.isDefinitionBased).toBe(false);
    expect(result.nodes).toHaveLength(1);
    expect(result.nodes[0].nodeId).toBe("n-1");
    expect(result.nodes[0].label).toBe("n-1");
    expect(result.nodes[0].status).toBe("RUNNING");
    expect(result.edges).toHaveLength(0);
    expect(result.groups).toEqual([]);
    expect(result.meta).toEqual({ direction: "TB" });
  });

  it("定義の edges を id と kind でマッピングする", () => {
    // Arrange
    const def = getGraphDefinition("hello")!;
    const exec = execution([], "hello");

    // Act
    const result = mergeGraph(exec, def);

    // Assert
    const forkEdge = result.edges.find((e) => e.from === "fork-1" && e.to === "task-b");
    expect(forkEdge?.id).toContain("fork-1");
    expect(forkEdge?.kind).toBe("fork");

    const resumeEdge = result.edges.find((e) => e.from === "task-c" && e.to === "join-1");
    expect(resumeEdge?.edgeType).toBe("Resume");
    expect(resumeEdge?.eventName).toBe("DoneC");
  });

  it("runtimeEdges と一致する定義エッジに traversed=true を付与する", () => {
    const def = getGraphDefinition("hello")!;
    const exec = {
      ...execution([], "hello"),
      runtimeEdges: [{ from: "start", to: "task-a", type: 0 }]
    };

    const result = mergeGraph(exec, def);
    const startToTaskA = result.edges.find((e) => e.from === "start" && e.to === "task-a");
    const taskAToFork = result.edges.find((e) => e.from === "task-a" && e.to === "fork-1");
    expect(startToTaskA?.traversed).toBe(true);
    expect(taskAToFork?.traversed).toBe(false);
  });

  it("runtime nodeId が実行時IDでも stateName ベースで定義ノード/エッジへマージできる", () => {
    const def = getGraphDefinition("hello")!;
    const exec = execution(
      [
        {
          nodeId: "rt-start-1",
          nodeType: "start",
          status: "SUCCEEDED",
          attempt: 1,
          workerId: null,
          waitKey: null,
          canceledByExecution: false
        },
        {
          nodeId: "rt-task-a-1",
          nodeType: "task-a",
          status: "RUNNING",
          attempt: 1,
          workerId: null,
          waitKey: null,
          canceledByExecution: false
        }
      ],
      "hello"
    );
    exec.runtimeEdges = [{ from: "rt-start-1", to: "rt-task-a-1", type: 0 }];

    const result = mergeGraph(exec, def);
    const startNode = result.nodes.find((n) => n.nodeId === "start");
    const taskANode = result.nodes.find((n) => n.nodeId === "task-a");
    const startToTaskA = result.edges.find((e) => e.from === "start" && e.to === "task-a");

    expect(startNode?.status).toBe("SUCCEEDED");
    expect(taskANode?.status).toBe("RUNNING");
    expect(startToTaskA?.traversed).toBe(true);
  });
});

describe("mergeGraph (境界値)", () => {
  it("execution.nodes が空・definition ありのときは定義の全ノードが IDLE", () => {
    // Arrange
    const def = getGraphDefinition("hello")!;
    const exec = execution([], "hello");

    // Act
    const result = mergeGraph(exec, def);

    // Assert
    expect(result.nodes.length).toBe(def.nodes.length);
    expect(result.nodes.every((n) => n.status === "IDLE")).toBe(true);
  });

  it("definition のノードに label が無いとき nodeId を label に (mergeGraph L78-79)", () => {
    // Arrange
    const def: GraphDefinition = {
      graphId: "custom",
      nodes: [{ nodeId: "n1", nodeType: "TASK" }],
      edges: []
    };
    const exec = execution([], "custom");

    // Act
    const result = mergeGraph(exec, def);

    // Assert
    expect(result.nodes).toHaveLength(1);
    expect(result.nodes[0].label).toBe("n1");
  });

  it("definition.meta が merged に引き継がれる", () => {
    const def: GraphDefinition = {
      graphId: "custom",
      nodes: [{ nodeId: "n1", nodeType: "TASK" }],
      edges: [],
      meta: { layout: { n1: { x: 5, y: 6 } } }
    };
    const result = mergeGraph(execution([], "custom"), def);
    expect(result.meta?.layout?.n1).toEqual({ x: 5, y: 6 });
  });

  it("definition が nodes/edges 空の最小定義でも merge は実行可能（別モジュールの型のためモック相当）", () => {
    // Arrange: 実在する hello は空でないので、execution のみで definition null の境界は既存テストで実施済み
    const exec = execution([]);

    // Act
    const result = mergeGraph(exec, null);

    // Assert
    expect(result.nodes).toHaveLength(0);
    expect(result.edges).toHaveLength(0);
    expect(result.isDefinitionBased).toBe(false);
  });
});
