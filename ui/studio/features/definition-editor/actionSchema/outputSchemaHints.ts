import type { JsonSchemaObject } from "./types";

/**
 * SimpleJsonPath の識別子セグメントとして有効か（英数字と `_` のみ）。
 */
export function isSimpleJsonPathIdentifier(segment: string): boolean {
  return /^\w+$/.test(segment);
}

/**
 * Execution Context 上の `$.states.<Name>.output.<prop>` パスを組み立てる。
 * ドット等を含む State / Node 名はブラケット＋単一引用にする。
 */
export function formatStateOutputPath(stateName: string, propertyName: string): string {
  const stateSegment = isSimpleJsonPathIdentifier(stateName)
    ? `.${stateName}`
    : `['${stateName}']`;
  const propertySegment = isSimpleJsonPathIdentifier(propertyName)
    ? `.${propertyName}`
    : `['${propertyName}']`;
  return `$.states${stateSegment}.output${propertySegment}`;
}

/**
 * outputSchema.properties から when.path 補完候補（Execution Context 根）を生成する。
 *
 * @param outputSchema - Action の outputSchema
 * @param stateName - 完了済み State / Node ID（パスの `states` セグメント）
 */
export function buildOutputSchemaPathHints(
  outputSchema: JsonSchemaObject | undefined | null,
  stateName: string
): string[] {
  if (!stateName) {
    return [];
  }
  const properties = outputSchema?.type === "object" ? (outputSchema.properties ?? {}) : {};
  return Object.keys(properties)
    .sort((a, b) => a.localeCompare(b))
    .map((name) => formatStateOutputPath(stateName, name));
}

/**
 * グラフ上の上流 action ノードの outputSchema から when.path 候補を収集する。
 */
export function collectUpstreamOutputPathHints(
  nodes: ReadonlyArray<{ name: string; type: string; action?: string }>,
  edges: ReadonlyArray<{ sourceId: string; targetId: string }>,
  targetNodeName: string,
  outputSchemaByActionId: ReadonlyMap<string, JsonSchemaObject | undefined>
): string[] {
  const upstreamActions = findUpstreamActionNodes(nodes, edges, targetNodeName);
  const hints = new Set<string>();
  for (const node of upstreamActions) {
    const actionId = node.action?.trim();
    if (!actionId) {
      continue;
    }
    for (const hint of buildOutputSchemaPathHints(outputSchemaByActionId.get(actionId), node.name)) {
      hints.add(hint);
    }
  }
  return [...hints].sort((a, b) => a.localeCompare(b));
}

function findUpstreamActionNodes(
  nodes: ReadonlyArray<{ name: string; type: string; action?: string }>,
  edges: ReadonlyArray<{ sourceId: string; targetId: string }>,
  targetNodeName: string
): Array<{ name: string; type: string; action?: string }> {
  const nodeByName = new Map(nodes.map((node) => [node.name, node]));
  const actionNodes: Array<{ name: string; type: string; action?: string }> = [];
  const visited = new Set<string>();
  const queue = edges
    .filter((edge) => edge.targetId === targetNodeName)
    .map((edge) => edge.sourceId);

  while (queue.length > 0) {
    const currentName = queue.shift();
    if (!currentName || visited.has(currentName)) {
      continue;
    }
    visited.add(currentName);
    const node = nodeByName.get(currentName);
    if (!node) {
      continue;
    }
    if (node.type === "action") {
      actionNodes.push(node);
    }
    for (const edge of edges) {
      if (edge.targetId === currentName) {
        queue.push(edge.sourceId);
      }
    }
  }

  return actionNodes;
}
