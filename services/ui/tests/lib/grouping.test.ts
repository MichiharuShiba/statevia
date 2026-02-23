import { describe, expect, it } from "vitest";
import { buildGroups, resolveGroupBounds } from "../../app/lib/grouping";
import type { ExecutionNodeDTO } from "../../app/lib/types";
import type { PositionedEdge, PositionedNode } from "../../app/lib/graphLayout";
import type { GraphGroupDef } from "../../app/graphs/types";

function execNodes(ids: string[]): ExecutionNodeDTO[] {
  return ids.map((nodeId) => ({
    nodeId,
    nodeType: "TASK",
    status: "IDLE" as const,
    attempt: 0,
    workerId: null,
    waitKey: null,
    canceledByExecution: false
  }));
}

function positioned(nodes: Array<{ nodeId: string; x: number; y: number; w: number; h: number }>): PositionedNode[] {
  return nodes.map((n) => ({
    ...n,
    nodeType: "TASK",
    w: n.w ?? 100,
    h: n.h ?? 50
  }));
}

describe("buildGroups", () => {
  it("fork-/join- の node id が無いとき空を返す", () => {
    // Arrange
    const nodes = execNodes(["task-a", "task-b"]);

    // Act
    const result = buildGroups(nodes);

    // Assert
    expect(result.groups).toHaveLength(0);
    expect(result.nodeToGroup).toEqual({});
  });

  it("最初の fork から最初の join まで 1 グループ返す", () => {
    // Arrange
    const nodes = execNodes(["task-a", "fork-1", "task-b", "join-1", "task-c"]);

    // Act
    const result = buildGroups(nodes);

    // Assert
    expect(result.groups).toHaveLength(1);
    expect(result.groups[0].groupId).toBe("fallback-fork-join");
    expect(result.groups[0].nodeIds).toEqual(["fork-1", "task-b", "join-1"]);
    expect(result.nodeToGroup["fork-1"]).toBe("fallback-fork-join");
    expect(result.nodeToGroup["join-1"]).toBe("fallback-fork-join");
  });

  it("fork の index が join 以上のとき空を返す", () => {
    // Arrange
    const nodes = execNodes(["join-1", "fork-1"]);

    // Act
    const result = buildGroups(nodes);

    // Assert
    expect(result.groups).toHaveLength(0);
  });

  it("境界値: nodes が空配列のとき groups と nodeToGroup は空", () => {
    // Arrange
    const nodes = execNodes([]);

    // Act
    const result = buildGroups(nodes);

    // Assert
    expect(result.groups).toHaveLength(0);
    expect(result.nodeToGroup).toEqual({});
  });

  it("境界値: ノードが 1 件だけのときは groups 空（fork/join なし）", () => {
    // Arrange
    const nodes = execNodes(["task-a"]);

    // Act
    const result = buildGroups(nodes);

    // Assert
    expect(result.groups).toHaveLength(0);
  });
});

describe("resolveGroupBounds", () => {
  it("定義グループが無く推論も空のとき空を返す", () => {
    // Arrange
    const nodes = positioned([{ nodeId: "a", x: 0, y: 0, w: 100, h: 50 }]);
    const edges: PositionedEdge[] = [];

    // Act
    const result = resolveGroupBounds(nodes, edges, undefined);

    // Assert
    expect(result).toHaveLength(0);
  });

  it("定義グループから bounds を計算する", () => {
    // Arrange
    const nodes = positioned([
      { nodeId: "n1", x: 10, y: 20, w: 100, h: 50 },
      { nodeId: "n2", x: 150, y: 20, w: 100, h: 50 }
    ]);
    const edges: PositionedEdge[] = [];
    const definitionGroups: GraphGroupDef[] = [
      { groupId: "g1", label: "Group 1", nodeIds: ["n1", "n2"] }
    ];

    // Act
    const result = resolveGroupBounds(nodes, edges, definitionGroups);

    // Assert
    expect(result).toHaveLength(1);
    expect(result[0].groupId).toBe("g1");
    expect(result[0].label).toBe("Group 1");
    expect(result[0].nodeIds).toEqual(["n1", "n2"]);
    expect(result[0].x).toBeLessThanOrEqual(10);
    expect(result[0].w).toBeGreaterThan(200);
    expect(result[0].h).toBeGreaterThan(50);
  });

  it("group の nodeIds が positionedNodes に無い場合はそのグループをスキップ (members.length === 0)", () => {
    // Arrange
    const nodes = positioned([{ nodeId: "n1", x: 10, y: 20, w: 100, h: 50 }]);
    const definitionGroups: GraphGroupDef[] = [
      { groupId: "g1", label: "Missing", nodeIds: ["not-present"] }
    ];

    // Act
    const result = resolveGroupBounds(nodes, [], definitionGroups);

    // Assert
    expect(result).toHaveLength(0);
  });

  it("hints から groupPadding を使う", () => {
    // Arrange
    const nodes = positioned([{ nodeId: "n1", x: 100, y: 100, w: 80, h: 40 }]);
    const definitionGroups: GraphGroupDef[] = [{ groupId: "g1", label: "G", nodeIds: ["n1"] }];

    // Act
    const result = resolveGroupBounds(nodes, [], definitionGroups, {
      groupPadding: { x: 10, y: 10, header: 20 }
    });

    // Assert
    expect(result).toHaveLength(1);
    expect(result[0].x).toBe(100 - 10);
    expect(result[0].y).toBe(100 - 10 - 20);
  });

  it("定義グループが空で fork/join があるときグラフからグループを推論する", () => {
    // Arrange: nodes with fork- and join- so inferGroupsFromGraph runs
    const nodes = positioned([
      { nodeId: "fork-1", x: 0, y: 0, w: 100, h: 50 },
      { nodeId: "task-b", x: 100, y: 0, w: 100, h: 50 },
      { nodeId: "task-c", x: 100, y: 60, w: 100, h: 50 },
      { nodeId: "join-1", x: 200, y: 30, w: 100, h: 50 }
    ]).map((n, i) => ({ ...n, nodeType: ["FORK", "TASK", "TASK", "JOIN"][i] }));
    const edges = [
      { id: "e1", from: "fork-1", to: "task-b" },
      { id: "e2", from: "fork-1", to: "task-c" },
      { id: "e3", from: "task-b", to: "join-1" },
      { id: "e4", from: "task-c", to: "join-1" }
    ];

    // Act
    const result = resolveGroupBounds(nodes, edges, undefined);

    // Assert
    expect(result.length).toBeGreaterThan(0);
    expect(result[0].label).toBe("Parallel Block");
    expect(result[0].nodeIds).toContain("fork-1");
    expect(result[0].nodeIds).toContain("join-1");
  });

  it("inferGroupsFromGraph: fork に outgoing が無いとき queue が空で groupNodes.length < 3 → null (L57, L73)", () => {
    // Arrange: fork-1 から出る edge なし
    const nodes = positioned([
      { nodeId: "fork-1", x: 0, y: 0, w: 100, h: 50 },
      { nodeId: "join-1", x: 200, y: 0, w: 100, h: 50 }
    ]).map((n, i) => ({ ...n, nodeType: ["FORK", "JOIN"][i] }));
    const edges: PositionedEdge[] = [];

    // Act
    const result = resolveGroupBounds(nodes, edges, undefined);

    // Assert
    expect(result).toHaveLength(0);
  });

  it("inferGroupsFromGraph: edge の先が nodeById に無いノードのとき continue (L62)", () => {
    // Arrange: fork -> ghost, fork -> task-b, task-b -> join. ghost は nodes に無い
    const nodes = positioned([
      { nodeId: "fork-1", x: 0, y: 0, w: 100, h: 50 },
      { nodeId: "task-b", x: 100, y: 0, w: 100, h: 50 },
      { nodeId: "join-1", x: 200, y: 0, w: 100, h: 50 }
    ]).map((n, i) => ({ ...n, nodeType: ["FORK", "TASK", "JOIN"][i] }));
    const edges: PositionedEdge[] = [
      { id: "e1", from: "fork-1", to: "ghost" },
      { id: "e2", from: "fork-1", to: "task-b" },
      { id: "e3", from: "task-b", to: "join-1" }
    ];

    // Act
    const result = resolveGroupBounds(nodes, edges, undefined);

    // Assert: ghost はスキップされ、fork/task-b/join の 3 ノードで 1 グループ
    expect(result).toHaveLength(1);
    expect(result[0].nodeIds).toContain("fork-1");
    expect(result[0].nodeIds).toContain("task-b");
    expect(result[0].nodeIds).toContain("join-1");
    expect(result[0].nodeIds).not.toContain("ghost");
  });
});
