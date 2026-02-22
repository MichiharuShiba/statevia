import type { GraphGroupDef, LayoutHints } from "../graphs/types";
import type { PositionedNode } from "./graphLayout";
import type { ExecutionDTO } from "./types";

export type GroupBounds = {
  groupId: string;
  label: string;
  nodeIds: string[];
  x: number;
  y: number;
  w: number;
  h: number;
};

export function buildGroups(nodes: ExecutionDTO["nodes"]): {
  groups: Array<{ groupId: string; label: string; nodeIds: string[] }>;
  nodeToGroup: Record<string, string>;
} {
  const ids = nodes.map((n) => n.nodeId);
  const firstForkIndex = ids.findIndex((id) => id.includes("fork-"));
  const firstJoinIndex = ids.findIndex((id) => id.includes("join-"));
  if (firstForkIndex < 0 || firstJoinIndex < 0 || firstForkIndex >= firstJoinIndex) {
    return { groups: [], nodeToGroup: {} };
  }

  const groupNodeIds = ids.slice(firstForkIndex, firstJoinIndex + 1);
  const groupId = "fallback-fork-join";
  const nodeToGroup = Object.fromEntries(groupNodeIds.map((nodeId) => [nodeId, groupId]));
  return {
    groups: [{ groupId, label: "Fork-Join Block", nodeIds: groupNodeIds }],
    nodeToGroup
  };
}

function inferGroupsFromPositionedNodes(nodes: PositionedNode[]): GraphGroupDef[] {
  const forkJoinNodeIds = nodes
    .filter((node) => node.nodeId.includes("fork-") || node.nodeId.includes("join-"))
    .map((node) => node.nodeId);
  if (forkJoinNodeIds.length < 2) return [];
  return [{ groupId: "inferred-parallel", label: "Parallel Block", nodeIds: forkJoinNodeIds }];
}

export function resolveGroupBounds(
  positionedNodes: PositionedNode[],
  definitionGroups: GraphGroupDef[] | undefined,
  hints?: LayoutHints
): GroupBounds[] {
  const groups = definitionGroups && definitionGroups.length > 0 ? definitionGroups : inferGroupsFromPositionedNodes(positionedNodes);
  if (groups.length === 0) return [];

  const paddingX = hints?.groupPadding?.x ?? 40;
  const paddingY = hints?.groupPadding?.y ?? 30;
  const header = hints?.groupPadding?.header ?? 28;
  const byId = new Map(positionedNodes.map((node) => [node.nodeId, node] as const));

  return groups
    .map((group) => {
      const members = group.nodeIds.map((id) => byId.get(id)).filter((m): m is PositionedNode => !!m);
      if (members.length === 0) return null;
      const minX = Math.min(...members.map((n) => n.x));
      const minY = Math.min(...members.map((n) => n.y));
      const maxX = Math.max(...members.map((n) => n.x + n.w));
      const maxY = Math.max(...members.map((n) => n.y + n.h));

      return {
        groupId: group.groupId,
        label: group.label,
        nodeIds: group.nodeIds,
        x: minX - paddingX,
        y: minY - paddingY - header,
        w: maxX - minX + paddingX * 2,
        h: maxY - minY + paddingY * 2 + header
      };
    })
    .filter((group): group is GroupBounds => !!group);
}
