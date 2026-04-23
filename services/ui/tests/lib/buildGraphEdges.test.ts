import { describe, expect, it } from "vitest";
import { buildGraphEdges } from "../../app/lib/buildGraphEdges";
import type { PositionedEdge } from "../../app/lib/graphLayout";

function edge(overrides: Partial<PositionedEdge> & { id: string; from: string; to: string }): PositionedEdge {
  const { id, from, to, ...rest } = overrides;
  return { id, from, to, ...rest };
}

describe("buildGraphEdges", () => {
  it("edgeType なしのとき Next として実線・ラベルなしで返す", () => {
    const edges: PositionedEdge[] = [edge({ id: "e1", from: "a", to: "b" })];
    const result = buildGraphEdges(edges);
    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({
      id: "e1",
      source: "a",
      target: "b",
      style: { stroke: "#d4d4d8", strokeWidth: 1.2 }
    });
    expect(result[0]).not.toHaveProperty("label");
  });

  it('edgeType "Next" のとき実線・ラベルなしで返す', () => {
    const edges: PositionedEdge[] = [edge({ id: "e1", from: "a", to: "b", edgeType: "Next" })];
    const result = buildGraphEdges(edges);
    expect(result[0]).toMatchObject({
      style: { stroke: "#d4d4d8", strokeWidth: 1.2 }
    });
    expect(result[0]).not.toHaveProperty("label");
  });

  it('edgeType "Resume" のとき破線とイベント名ラベルで返す', () => {
    const edges: PositionedEdge[] = [
      edge({ id: "e1", from: "wait", to: "join", edgeType: "Resume", eventName: "DoneC" })
    ];
    const result = buildGraphEdges(edges);
    expect(result[0]).toMatchObject({
      id: "e1",
      source: "wait",
      target: "join",
      style: { stroke: "#78716c", strokeWidth: 1.2, strokeDasharray: "8 4" },
      label: "DoneC",
      labelStyle: { fontSize: 10, fontWeight: 600 },
      labelBgStyle: { fill: "#fafaf9" },
      labelBgBorderRadius: 4
    });
  });

  it('edgeType "Resume" で eventName がないときラベルは "Resume"', () => {
    const edges: PositionedEdge[] = [edge({ id: "e1", from: "a", to: "b", edgeType: "Resume" })];
    const result = buildGraphEdges(edges);
    expect(result[0].label).toBe("Resume");
    expect(result[0]).toMatchObject({ style: { strokeDasharray: "8 4" } });
  });

  it('edgeType "Cancel" のとき太線と "Cancel" ラベルで返す', () => {
    const edges: PositionedEdge[] = [
      edge({ id: "e1", from: "task", to: "end", edgeType: "Cancel", cancelReason: "UserRequest" })
    ];
    const result = buildGraphEdges(edges);
    expect(result[0]).toMatchObject({
      id: "e1",
      source: "task",
      target: "end",
      style: { stroke: "#b91c1c", strokeWidth: 2.5 },
      label: "Cancel",
      labelStyle: { fontSize: 10, fontWeight: 700, fill: "#b91c1c" },
      labelBgStyle: { fill: "#fef2f2" },
      labelBgBorderRadius: 4
    });
  });

  it("複数エッジで Next / Resume / Cancel が混在するとき種別ごとに正しく変換する", () => {
    const edges: PositionedEdge[] = [
      edge({ id: "e1", from: "a", to: "b" }),
      edge({ id: "e2", from: "b", to: "c", edgeType: "Resume", eventName: "Ev" }),
      edge({ id: "e3", from: "c", to: "d", edgeType: "Cancel" })
    ];
    const result = buildGraphEdges(edges);
    expect(result).toHaveLength(3);
    expect(result[0].style).toEqual({ stroke: "#d4d4d8", strokeWidth: 1.2 });
    expect(result[0].label).toBeUndefined();
    expect(result[1].style).toMatchObject({ strokeDasharray: "8 4" });
    expect(result[1].label).toBe("Ev");
    expect(result[2].style).toEqual({ stroke: "#b91c1c", strokeWidth: 2.5 });
    expect(result[2].label).toBe("Cancel");
  });

  it("全エッジに markerEnd と animated: false を付与する", () => {
    const edges: PositionedEdge[] = [edge({ id: "e1", from: "a", to: "b" })];
    const result = buildGraphEdges(edges);
    expect(result[0].markerEnd).toMatchObject({ width: 14, height: 14 });
    expect(result[0].markerEnd).toHaveProperty("type");
    expect(result[0].animated).toBe(false);
  });
});
