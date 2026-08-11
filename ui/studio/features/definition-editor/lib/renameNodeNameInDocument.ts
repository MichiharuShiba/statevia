import type { DefinitionGraphDocument, DefinitionGraphNode } from "./types";

function refTargetsRenamedName(value: string | undefined, fromName: string): boolean {
  return value?.trim() === fromName;
}

/**
 * ノード名を変更し、`next` / `edges[].to` / fork の `branches[]` /
 * wait の `events` 遷移先および `meta.layout` のキーを同期する。
 *
 * `edges[].to` はパース後は常に文字列（`to: { name }` は解決済み）を前提とする。
 */
export function renameNodeNameInDocument(
  document: DefinitionGraphDocument,
  fromName: string,
  toName: string
): DefinitionGraphDocument {
  if (fromName === toName) {
    return document;
  }

  const nodes: DefinitionGraphNode[] = document.nodes.map((node) => {
    const name = node.name === fromName ? toName : node.name;
    const next = refTargetsRenamedName(node.next, fromName) ? toName : node.next;
    const error = refTargetsRenamedName(node.error, fromName) ? toName : node.error;
    const branches = node.branches?.map((b) => (refTargetsRenamedName(b, fromName) ? toName : b));
    const edges = node.edges?.map((e) => (refTargetsRenamedName(e.to, fromName) ? { ...e, to: toName } : e));
    const events =
      node.events === undefined
        ? undefined
        : Object.fromEntries(
            Object.entries(node.events).map(([eventName, target]) => [
              eventName,
              refTargetsRenamedName(target, fromName) ? toName : target
            ])
          );

    return {
      ...node,
      name,
      next,
      error,
      branches,
      edges,
      events
    };
  });

  let meta = document.meta;
  const layout = meta?.layout;
  if (layout && fromName in layout) {
    const nextLayout = { ...layout };
    const pos = nextLayout[fromName];
    delete nextLayout[fromName];
    nextLayout[toName] = pos;
    meta = { ...meta, layout: nextLayout };
  }

  return { ...document, nodes, meta };
}
