import type { JsonSchemaObject } from "./types";

/**
 * outputSchema.properties から when.path 補完候補（`$.` 付き）を生成する。
 */
export function buildOutputSchemaPathHints(outputSchema: JsonSchemaObject | undefined | null): string[] {
  if (!outputSchema || outputSchema.type !== "object") {
    return [];
  }
  const properties = outputSchema.properties ?? {};
  return Object.keys(properties)
    .sort((a, b) => a.localeCompare(b))
    .map((name) => `$.${name}`);
}

/**
 * グラフ上の直前 action ノードの outputSchema から when.path 候補を収集する。
 */
export function collectUpstreamOutputPathHints(
  nodes: ReadonlyArray<{ id: string; type: string; action?: string }>,
  edges: ReadonlyArray<{ sourceId: string; targetId: string }>,
  targetNodeId: string,
  outputSchemaByActionId: ReadonlyMap<string, JsonSchemaObject | undefined>
): string[] {
  const upstreamActionIds = findUpstreamActionIds(nodes, edges, targetNodeId);
  const hints = new Set<string>();
  for (const actionId of upstreamActionIds) {
    for (const hint of buildOutputSchemaPathHints(outputSchemaByActionId.get(actionId))) {
      hints.add(hint);
    }
  }
  return [...hints].sort((a, b) => a.localeCompare(b));
}

function findUpstreamActionIds(
  nodes: ReadonlyArray<{ id: string; type: string; action?: string }>,
  edges: ReadonlyArray<{ sourceId: string; targetId: string }>,
  targetNodeId: string
): string[] {
  const nodeById = new Map(nodes.map((node) => [node.id, node]));
  const actionIds: string[] = [];
  const visited = new Set<string>();
  const queue = edges
    .filter((edge) => edge.targetId === targetNodeId)
    .map((edge) => edge.sourceId);

  while (queue.length > 0) {
    const currentId = queue.shift();
    if (!currentId || visited.has(currentId)) {
      continue;
    }
    visited.add(currentId);
    const node = nodeById.get(currentId);
    if (!node) {
      continue;
    }
    if (node.type === "action" && node.action && node.action.trim().length > 0) {
      actionIds.push(node.action.trim());
    }
    for (const edge of edges) {
      if (edge.targetId === currentId) {
        queue.push(edge.sourceId);
      }
    }
  }

  return actionIds;
}
