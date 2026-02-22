import dagre from "dagre";
import type { LayoutHints } from "../graphs/types";

export type LayoutNodeInput = {
  nodeId: string;
  nodeType: string;
  branch?: string;
};

export type LayoutEdgeInput = {
  id: string;
  from: string;
  to: string;
  kind?: "normal" | "fork" | "join";
};

export type PositionedNode<T extends LayoutNodeInput = LayoutNodeInput> = T & {
  x: number;
  y: number;
  w: number;
  h: number;
};

export type PositionedEdge = LayoutEdgeInput;

function normalizeType(nodeType: string): string {
  return nodeType.trim().toUpperCase();
}

function getNodeSize(nodeType: string): { w: number; h: number } {
  const type = normalizeType(nodeType);
  if (type === "START" || type === "SUCCESS" || type === "COMPLETED" || type === "SUCCEEDED") {
    return { w: 180, h: 70 };
  }
  if (type === "WAIT" || type === "WAITING") {
    return { w: 240, h: 150 };
  }
  if (type === "JOIN") {
    return { w: 200, h: 120 };
  }
  return { w: 240, h: 120 };
}

function getFallbackSortWeight(nodeType: string): number {
  const type = normalizeType(nodeType);
  if (type === "START") return 10;
  if (type === "TASK" || type === "WAIT" || type === "WAITING") return 20;
  if (type === "FORK") return 30;
  if (type === "JOIN") return 40;
  if (type === "SUCCESS" || type === "SUCCEEDED" || type === "COMPLETED" || type === "FAILED" || type === "CANCELED") {
    return 50;
  }
  return 35;
}

export function buildFallbackEdges(nodes: LayoutNodeInput[]): LayoutEdgeInput[] {
  const sorted = [...nodes].sort((a, b) => getFallbackSortWeight(a.nodeType) - getFallbackSortWeight(b.nodeType));
  const edges: LayoutEdgeInput[] = [];
  for (let i = 0; i < sorted.length - 1; i += 1) {
    edges.push({
      id: `fallback-${sorted[i].nodeId}-${sorted[i + 1].nodeId}`,
      from: sorted[i].nodeId,
      to: sorted[i + 1].nodeId,
      kind: "normal"
    });
  }

  const fork = sorted.find((n) => normalizeType(n.nodeType) === "FORK");
  const join = sorted.find((n) => normalizeType(n.nodeType) === "JOIN");
  if (!fork) return edges;

  const afterFork = sorted.filter((n) => n.nodeId !== fork.nodeId);
  const branches = afterFork.slice(0, 2);
  if (branches.length === 2) {
    edges.push(
      { id: `fallback-fork-${fork.nodeId}-${branches[0].nodeId}`, from: fork.nodeId, to: branches[0].nodeId, kind: "fork" },
      { id: `fallback-fork-${fork.nodeId}-${branches[1].nodeId}`, from: fork.nodeId, to: branches[1].nodeId, kind: "fork" }
    );
    if (join) {
      edges.push(
        { id: `fallback-join-${branches[0].nodeId}-${join.nodeId}`, from: branches[0].nodeId, to: join.nodeId, kind: "join" },
        { id: `fallback-join-${branches[1].nodeId}-${join.nodeId}`, from: branches[1].nodeId, to: join.nodeId, kind: "join" }
      );
    }
  }
  return edges;
}

function applyBranchOffsets<T extends LayoutNodeInput>(nodes: Array<PositionedNode<T>>, hints?: LayoutHints): Array<PositionedNode<T>> {
  const branchIds = Array.from(new Set(nodes.map((n) => n.branch).filter((b): b is string => !!b)));
  if (branchIds.length === 0) return nodes;

  const order = hints?.branchOrder?.length
    ? hints.branchOrder
    : branchIds.sort((a, b) => a.localeCompare(b));
  const offsetByBranch = new Map(order.map((branch, index) => [branch, index * 220]));
  return nodes.map((node) => {
    const offset = node.branch ? offsetByBranch.get(node.branch) ?? 0 : 0;
    return { ...node, y: node.y + offset };
  });
}

export function layoutGraph<T extends LayoutNodeInput>(nodes: T[], rawEdges: LayoutEdgeInput[], hints?: LayoutHints): {
  nodes: Array<PositionedNode<T>>;
  edges: PositionedEdge[];
} {
  const edges = rawEdges.length > 0 ? rawEdges : buildFallbackEdges(nodes);
  const graph = new dagre.graphlib.Graph();
  graph.setGraph({
    rankdir: hints?.direction ?? "LR",
    ranksep: 90,
    nodesep: 50,
    marginx: 20,
    marginy: 20
  });
  graph.setDefaultEdgeLabel(() => ({}));

  nodes.forEach((node) => {
    const baseSize = getNodeSize(node.nodeType);
    const override = hints?.nodeSizeOverrides?.[node.nodeId];
    graph.setNode(node.nodeId, {
      width: override?.w ?? baseSize.w,
      height: override?.h ?? baseSize.h
    });
  });

  edges.forEach((edge) => {
    graph.setEdge(edge.from, edge.to);
  });

  dagre.layout(graph);

  const positioned: Array<PositionedNode<T>> = nodes.map((node) => {
    const gNode = graph.node(node.nodeId) as { x: number; y: number; width: number; height: number };
    return {
      ...node,
      x: gNode.x - gNode.width / 2,
      y: gNode.y - gNode.height / 2,
      w: gNode.width,
      h: gNode.height
    };
  });

  return { nodes: applyBranchOffsets(positioned, hints), edges };
}

