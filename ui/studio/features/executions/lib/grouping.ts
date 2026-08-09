import type { GraphDefinitionMeta, GraphGroupDef } from "@/features/executions/graphs/types";
import type { LayoutEdgeInput, PositionedNode } from "@/shared/lib/graphLayout";
import type { ExecutionView } from "../types";

/** グループ矩形の境界。 */
export type GroupBounds = {
  groupId: string;
  label: string;
  nodeNames: string[];
  x: number;
  y: number;
  w: number;
  h: number;
};

/** ノード一覧からグループ境界を構築する。 */
export function buildGroups(nodes: ExecutionView["nodes"]): {
  groups: Array<{ groupId: string; label: string; nodeIds: string[] }>;
  nodeToGroup: Record<string, string>;
} {
  const ids = nodes.map((n) => n.executionNodeId);
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

function inferGroupsFromGraph(nodes: PositionedNode[], edges: LayoutEdgeInput[]): GraphGroupDef[] {
  const nodeByName = new Map(nodes.map((node) => [node.name, node] as const));
  const forks = nodes.filter(
    (node) => node.name.includes("fork-") || node.nodeType.trim().toUpperCase() === "FORK"
  );
  const joins = new Set(
    nodes
      .filter((node) => node.name.includes("join-") || node.nodeType.trim().toUpperCase() === "JOIN")
      .map((node) => node.name)
  );
  if (forks.length === 0 || joins.size === 0) return [];

  const outgoing = new Map<string, string[]>();
  edges.forEach((edge) => {
    const list = outgoing.get(edge.from) ?? [];
    list.push(edge.to);
    outgoing.set(edge.from, list);
  });

  return forks
    .map((forkNode, index) => {
      const visited = new Set<string>([forkNode.name]);
      const queue = [...(outgoing.get(forkNode.name) ?? [])];

      while (queue.length > 0) {
        const current = queue.shift();
        if (!current || visited.has(current)) continue;
        if (!nodeByName.has(current)) continue;

        visited.add(current);
        if (joins.has(current)) continue;

        const next = outgoing.get(current) ?? [];
        next.forEach((to) => {
          if (!visited.has(to)) queue.push(to);
        });
      }

      const groupNodes = Array.from(visited).filter((name) => nodeByName.has(name));
      if (groupNodes.length < 3) return null;

      return {
        groupId: `inferred-parallel-${index + 1}`,
        label: "Parallel Block",
        nodeNames: groupNodes
      } satisfies GraphGroupDef;
    })
    .filter((group): group is GraphGroupDef => !!group);
}

/** グループ ID から境界矩形を解決する。 */
export function resolveGroupBounds(
  positionedNodes: PositionedNode[],
  positionedEdges: LayoutEdgeInput[],
  definitionGroups: GraphGroupDef[] | undefined,
  hints?: GraphDefinitionMeta
): GroupBounds[] {
  const groups =
    definitionGroups && definitionGroups.length > 0
      ? definitionGroups
      : inferGroupsFromGraph(positionedNodes, positionedEdges);
  if (groups.length === 0) return [];

  const paddingX = hints?.groupPadding?.x ?? 40;
  const paddingY = hints?.groupPadding?.y ?? 30;
  const header = hints?.groupPadding?.header ?? 28;
  const byName = new Map(positionedNodes.map((node) => [node.name, node] as const));

  return groups
    .map((group) => {
      const members = group.nodeNames.map((name) => byName.get(name)).filter((m): m is PositionedNode => !!m);
      if (members.length === 0) return null;
      const minX = Math.min(...members.map((n) => n.x));
      const minY = Math.min(...members.map((n) => n.y));
      const maxX = Math.max(...members.map((n) => n.x + n.w));
      const maxY = Math.max(...members.map((n) => n.y + n.h));

      return {
        groupId: group.groupId,
        label: group.label,
        nodeNames: group.nodeNames,
        x: minX - paddingX,
        y: minY - paddingY - header,
        w: maxX - minX + paddingX * 2,
        h: maxY - minY + paddingY * 2 + header
      };
    })
    .filter((group): group is GroupBounds => !!group);
}
