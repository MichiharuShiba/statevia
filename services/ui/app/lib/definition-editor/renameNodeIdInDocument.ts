import type { DefinitionGraphDocument, DefinitionGraphNode } from "./types";

function refTargetsRenamedId(value: string | undefined, fromId: string): boolean {
  return value?.trim() === fromId;
}

/**
 * ノード ID を変更し、`next` / `edges[].to` / fork の `branches[]` および `meta.layout` のキーを同期する。
 */
export function renameNodeIdInDocument(
  document: DefinitionGraphDocument,
  fromId: string,
  toId: string
): DefinitionGraphDocument {
  if (fromId === toId) {
    return document;
  }

  const nodes: DefinitionGraphNode[] = document.nodes.map((node) => {
    const id = node.id === fromId ? toId : node.id;
    const next = refTargetsRenamedId(node.next, fromId) ? toId : node.next;
    const branches = node.branches?.map((b) => (refTargetsRenamedId(b, fromId) ? toId : b));
    const edges = node.edges?.map((e) => (refTargetsRenamedId(e.to, fromId) ? { ...e, to: toId } : e));

    return {
      ...node,
      id,
      next,
      branches,
      edges
    };
  });

  let meta = document.meta;
  const layout = meta?.layout;
  if (layout && fromId in layout) {
    const nextLayout = { ...layout };
    const pos = nextLayout[fromId];
    delete nextLayout[fromId];
    nextLayout[toId] = pos;
    meta = { ...meta, layout: nextLayout };
  }

  return { ...document, nodes, meta };
}
