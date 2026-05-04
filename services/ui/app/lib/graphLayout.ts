import dagre from "dagre";
import type { GraphDefinitionMeta } from "../graphs/types";

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
  edgeType?: "Next" | "Resume" | "Cancel";
  eventName?: string;
  cancelReason?: string;
  cancelCause?: string;
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

/** アクションと同じ横幅（全ノード統一） */
const STANDARD_NODE_WIDTH = 240;
const STANDARD_NODE_HEIGHT = 120;

function getNodeSize(nodeType: string, hints?: GraphDefinitionMeta): { w: number; h: number } {
  const type = normalizeType(nodeType);
  if (hints?.defaultNodeSize) {
    return { w: hints.defaultNodeSize.w, h: hints.defaultNodeSize.h };
  }
  if (type === "WAIT" || type === "WAITING") {
    return { w: STANDARD_NODE_WIDTH, h: 150 };
  }
  return { w: STANDARD_NODE_WIDTH, h: STANDARD_NODE_HEIGHT };
}

function getFallbackSortWeight(nodeType: string): number {
  const type = normalizeType(nodeType);
  if (type === "START") return 10;
  if (type === "TASK" || type === "ACTION" || type === "WAIT" || type === "WAITING") return 20;
  if (type === "FORK") return 30;
  if (type === "JOIN") return 40;
  if (type === "SUCCESS" || type === "SUCCEEDED" || type === "COMPLETED" || type === "END" || type === "FAILED" || type === "CANCELED") {
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

function isVerticalFlow(rankdir: string): boolean {
  return rankdir === "TB" || rankdir === "BT";
}

function applyBranchOffsets<T extends LayoutNodeInput>(
  nodes: Array<PositionedNode<T>>,
  hints: GraphDefinitionMeta | undefined,
  rankdir: string
): Array<PositionedNode<T>> {
  const branchIds = Array.from(new Set(nodes.map((n) => n.branch).filter((b): b is string => !!b)));
  if (branchIds.length === 0) return nodes;

  const order =
    hints?.branchOrder?.length
      ? hints.branchOrder
      : [...branchIds].sort((a, b) => a.localeCompare(b));
  const offsetByBranch = new Map(order.map((branch, index) => [branch, index * 220]));
  return nodes.map((node) => {
    const offset = node.branch ? offsetByBranch.get(node.branch) ?? 0 : 0;
    if (isVerticalFlow(rankdir)) {
      return { ...node, x: node.x + offset };
    }
    return { ...node, y: node.y + offset };
  });
}

export function layoutGraph<T extends LayoutNodeInput>(nodes: T[], rawEdges: LayoutEdgeInput[], hints?: GraphDefinitionMeta): {
  nodes: Array<PositionedNode<T>>;
  edges: PositionedEdge[];
} {
  const edges = rawEdges.length > 0 ? rawEdges : buildFallbackEdges(nodes);
  const nodeIdSet = new Set(nodes.map((n) => n.nodeId));
  const safeEdges = edges.filter(
    (edge) =>
      edge.from.length > 0 &&
      edge.to.length > 0 &&
      nodeIdSet.has(edge.from) &&
      nodeIdSet.has(edge.to)
  );
  const rankdir = hints?.direction ?? "TB";
  const compact =
    hints?.defaultNodeSize != null && hints.defaultNodeSize.h > 0 && hints.defaultNodeSize.h <= 72;
  const graph = new dagre.graphlib.Graph();
  graph.setGraph({
    rankdir,
    ranksep: compact ? 44 : 90,
    nodesep: compact ? 36 : 50,
    marginx: 20,
    marginy: 20
  });
  // dagre の rank/network-simplex は各エッジに minlen・weight が必須（未設定だと layout 中に落ちる）。
  graph.setDefaultEdgeLabel(() => ({ minlen: 1, weight: 1 }));

  nodes.forEach((node) => {
    const baseSize = getNodeSize(node.nodeType, hints);
    const override = hints?.nodeSizeOverrides?.[node.nodeId];
    graph.setNode(node.nodeId, {
      width: override?.w ?? baseSize.w,
      height: override?.h ?? baseSize.h
    });
  });

  safeEdges.forEach((edge) => {
    graph.setEdge(edge.from, edge.to, { minlen: 1, weight: 1 });
  });

  // graphlib の既存エッジ更新時にラベルが欠落した場合の保険（network-simplex の simplify が label.weight を参照する）。
  graph.edges().forEach((e) => {
    const lab = graph.edge(e) as { minlen?: number; weight?: number } | undefined;
    if (!lab || typeof lab.minlen !== "number" || typeof lab.weight !== "number") {
      graph.setEdge(e, {
        minlen: typeof lab?.minlen === "number" ? lab.minlen : 1,
        weight: typeof lab?.weight === "number" ? lab.weight : 1
      });
    }
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

  return { nodes: applyBranchOffsets(positioned, hints, rankdir), edges: safeEdges };
}

