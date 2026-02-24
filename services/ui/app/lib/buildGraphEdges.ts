import { MarkerType, type Edge } from "reactflow";
import type { PositionedEdge } from "./graphLayout";

/**
 * PositionedEdge を React Flow の Edge に変換する。
 * 仕様: Next=実線, Resume=破線+イベント名, Cancel=太線+Cancel表示
 */
export function buildGraphEdges(edges: PositionedEdge[]): Edge[] {
  return edges.map((edge) => {
    const edgeType = edge.edgeType ?? "Next";
    const base = {
      id: edge.id,
      source: edge.from,
      target: edge.to,
      markerEnd: { type: MarkerType.ArrowClosed, width: 14, height: 14 },
      animated: false
    };
    if (edgeType === "Resume") {
      return {
        ...base,
        style: { stroke: "#78716c", strokeWidth: 1.2, strokeDasharray: "8 4" },
        label: edge.eventName ?? "Resume",
        labelStyle: { fontSize: 10, fontWeight: 600 },
        labelBgStyle: { fill: "#fafaf9" },
        labelBgPadding: [4, 2] as [number, number],
        labelBgBorderRadius: 4
      };
    }
    if (edgeType === "Cancel") {
      return {
        ...base,
        style: { stroke: "#b91c1c", strokeWidth: 2.5 },
        label: "Cancel",
        labelStyle: { fontSize: 10, fontWeight: 700, fill: "#b91c1c" },
        labelBgStyle: { fill: "#fef2f2" },
        labelBgPadding: [4, 2] as [number, number],
        labelBgBorderRadius: 4
      };
    }
    return {
      ...base,
      style: { stroke: "#d4d4d8", strokeWidth: 1.2 }
    };
  });
}
