import type { DefinitionGraphDocument, DefinitionGraphNode } from "./types";

/** グラフ上の有向辺（source → target）。 */
export type GraphAdjacencyEdge = {
  sourceId: string;
  targetId: string;
};

/**
 * 単一ノードから出る辺を adjacency リストへ追加する。
 *
 * @remarks wait.`events` の遷移先も含める（キャンバス辺・上流ヒント用）。
 */
function appendWaitEventOutgoingEdges(node: DefinitionGraphNode, edges: GraphAdjacencyEdge[]): void {
  if (node.type !== "wait" || !node.events) {
    return;
  }
  for (const target of Object.values(node.events)) {
    const trimmed = target?.trim();
    if (trimmed) {
      edges.push({ sourceId: node.name, targetId: trimmed });
    }
  }
}

function appendNodeOutgoingEdges(node: DefinitionGraphNode, edges: GraphAdjacencyEdge[]): void {
  if (node.next) {
    edges.push({ sourceId: node.name, targetId: node.next });
  }
  if (node.type === "action" && node.error) {
    edges.push({ sourceId: node.name, targetId: node.error });
  }
  for (const edge of node.edges ?? []) {
    if (edge.to) {
      edges.push({ sourceId: node.name, targetId: edge.to });
    }
  }
  if (node.type === "fork") {
    for (const branch of node.branches ?? []) {
      edges.push({ sourceId: node.name, targetId: branch });
    }
  }
  appendWaitEventOutgoingEdges(node, edges);
}

/**
 * DefinitionGraphDocument から有向辺一覧を構築する（when.path 補完等に利用）。
 */
export function buildDocumentAdjacency(document: DefinitionGraphDocument): GraphAdjacencyEdge[] {
  const edges: GraphAdjacencyEdge[] = [];
  for (const node of document.nodes) {
    appendNodeOutgoingEdges(node, edges);
  }
  return edges;
}
