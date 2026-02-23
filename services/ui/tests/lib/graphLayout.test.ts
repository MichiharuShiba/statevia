import { describe, expect, it } from "vitest";
import { layoutGraph, buildFallbackEdges } from "../../app/lib/graphLayout";
import type { LayoutNodeInput } from "../../app/lib/graphLayout";

const node = (nodeId: string, nodeType: string, branch?: string): LayoutNodeInput => ({
  nodeId,
  nodeType,
  ...(branch && { branch })
});

describe("buildFallbackEdges", () => {
  it("fork/join が無いときソート順でノードを繋ぐ", () => {
    // Arrange
    const nodes = [node("a", "TASK"), node("b", "TASK"), node("c", "TASK")];

    // Act
    const edges = buildFallbackEdges(nodes);

    // Assert
    expect(edges.length).toBe(2);
    expect(edges[0]).toMatchObject({ from: "a", to: "b", kind: "normal" });
    expect(edges[1]).toMatchObject({ from: "b", to: "c", kind: "normal" });
  });

  it("fork と join があるとき fork/join 用 edge を追加する", () => {
    // Arrange
    const nodes = [
      node("start", "Start"),
      node("f", "Fork"),
      node("b1", "Task", "b"),
      node("b2", "Wait", "c"),
      node("j", "Join")
    ];

    // Act
    const edges = buildFallbackEdges(nodes);

    // Assert
    expect(edges.some((e) => e.from === "f" && e.kind === "fork")).toBe(true);
    expect(edges.some((e) => e.to === "j" && e.kind === "join")).toBe(true);
  });

  it("Success/FAILED/CANCELED は重み 50、未知タイプは 35 でソートする", () => {
    // Arrange
    const nodes = [node("end", "Success"), node("mid", "UnknownType"), node("fail", "FAILED")];

    // Act
    const edges = buildFallbackEdges(nodes);

    // Assert
    expect(edges.length).toBeGreaterThan(0);
    expect(edges.some((e) => e.from === "mid" || e.to === "mid")).toBe(true);
  });
});

describe("layoutGraph", () => {
  it("ノードを配置して edges を返す", () => {
    // Arrange
    const nodes = [node("a", "Task"), node("b", "Task")];
    const rawEdges = [{ id: "e-1", from: "a", to: "b" }];

    // Act
    const result = layoutGraph(nodes, rawEdges);

    // Assert
    expect(result.nodes).toHaveLength(2);
    expect(result.edges).toHaveLength(1);
    result.nodes.forEach((n) => {
      expect("x" in n && "y" in n && "w" in n && "h" in n).toBe(true);
      expect((n as { w: number }).w).toBeGreaterThan(0);
      expect((n as { h: number }).h).toBeGreaterThan(0);
    });
  });

  it("rawEdges が空のとき buildFallbackEdges を使う", () => {
    // Arrange
    const nodes = [node("a", "Task"), node("b", "Task")];

    // Act
    const result = layoutGraph(nodes, []);

    // Assert
    expect(result.edges.length).toBeGreaterThan(0);
    expect(result.nodes).toHaveLength(2);
  });

  it("hints から direction を適用する", () => {
    // Arrange
    const nodes = [node("a", "Task"), node("b", "Task")];
    const rawEdges = [{ id: "e-1", from: "a", to: "b" }];

    // Act
    const result = layoutGraph(nodes, rawEdges, { direction: "LR" });

    // Assert
    expect(result.nodes).toHaveLength(2);
  });

  it("hints に branchOrder があるとき branchOffset を適用する", () => {
    // Arrange
    const nodes = [
      { ...node("a", "Task"), branch: "b" as string },
      { ...node("c", "Task"), branch: "c" as string }
    ];
    const rawEdges = [{ id: "e-1", from: "a", to: "c" }];

    // Act
    const result = layoutGraph(nodes, rawEdges, { branchOrder: ["b", "c"] });

    // Assert
    expect(result.nodes).toHaveLength(2);
    const withBranch = result.nodes.filter((n) => (n as { branch?: string }).branch);
    expect(withBranch.length).toBeGreaterThan(0);
  });

  it("branchOrder に無い branch は offset 0 にする", () => {
    // Arrange
    const nodes = [
      { ...node("a", "Task"), branch: "other" as string },
      { ...node("b", "Task"), branch: "b" as string }
    ];
    const rawEdges = [{ id: "e-1", from: "a", to: "b" }];

    // Act
    const result = layoutGraph(nodes, rawEdges, { branchOrder: ["b"] });

    // Assert
    expect(result.nodes).toHaveLength(2);
    const otherNode = result.nodes.find((n) => (n as { branch?: string }).branch === "other");
    expect(otherNode).toBeDefined();
  });

  it("branch なしノードは offset 0、branch ありは offset 適用 (graphLayout L95 全分岐)", () => {
    // Arrange: 片方だけ branch あり
    const nodes = [
      node("a", "Task"),
      { ...node("b", "Task"), branch: "b" as string }
    ];
    const rawEdges = [{ id: "e-1", from: "a", to: "b" }];

    // Act
    const result = layoutGraph(nodes, rawEdges, { branchOrder: ["b"] });

    // Assert
    expect(result.nodes).toHaveLength(2);
    const noBranch = result.nodes.find((n) => (n as { branch?: string }).branch === undefined);
    expect(noBranch).toBeDefined();
  });

  it("hints に branchOrder が無いとき branchIds.sort で順序を決める (L93-95)", () => {
    // Arrange: branch あり、hints はあるが branchOrder なし
    const nodes = [
      { ...node("a", "Task"), branch: "z" as string },
      { ...node("b", "Task"), branch: "a" as string }
    ];
    const rawEdges = [{ id: "e-1", from: "a", to: "b" }];

    // Act
    const result = layoutGraph(nodes, rawEdges, { direction: "LR" });

    // Assert
    expect(result.nodes).toHaveLength(2);
  });
});

describe("layoutGraph (境界値)", () => {
  it("ノードが 1 件だけのとき edges は空で positions のみ返す", () => {
    // Arrange
    const nodes = [node("single", "Task")];
    const rawEdges: { id: string; from: string; to: string }[] = [];

    // Act
    const result = layoutGraph(nodes, rawEdges);

    // Assert
    expect(result.nodes).toHaveLength(1);
    expect(result.nodes[0].nodeId).toBe("single");
    expect(result.edges).toHaveLength(0);
    expect((result.nodes[0] as { x: number }).x).toBeDefined();
    expect((result.nodes[0] as { y: number }).y).toBeDefined();
  });

  it("ノードが 0 件のときは空配列を返す", () => {
    // Arrange
    const nodes: LayoutNodeInput[] = [];
    const rawEdges: { id: string; from: string; to: string }[] = [];

    // Act
    const result = layoutGraph(nodes, rawEdges);

    // Assert
    expect(result.nodes).toHaveLength(0);
    expect(result.edges).toHaveLength(0);
  });
});
