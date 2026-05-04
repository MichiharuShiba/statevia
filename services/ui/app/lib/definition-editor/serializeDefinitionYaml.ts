import { stringify } from "yaml";
import type { DefinitionGraphDocument, DefinitionGraphEdge } from "./types";

/**
 * DefinitionGraphDocument を既存 nodes スキーマの YAML へ変換する。
 */
export function serializeDefinitionYaml(document: DefinitionGraphDocument): string {
  const nodes = document.nodes.map((node) => {
    const base: Record<string, unknown> = {
      id: node.id,
      type: node.type
    };

    if (node.action?.trim()) {
      base.action = node.action.trim();
    }
    if (node.event?.trim()) {
      base.event = node.event.trim();
    }
    if (node.type === "fork") {
      base.branches = [...(node.branches ?? [])];
    }
    if (node.type === "join") {
      base.mode = "all";
    }
    if (node.input && Object.keys(node.input).length > 0) {
      base.input = node.input;
    }

    const edges: DefinitionGraphEdge[] = (node.edges ?? [])
      .map((edge) => ({
        to: edge.to,
        ...(edge.when
          ? {
              when: {
                path: edge.when.path,
                op: edge.when.op,
                value: edge.when.value
              }
            }
          : {}),
        ...(typeof edge.order === "number" ? { order: edge.order } : {}),
        ...(edge.default === true ? { default: true } : {})
      }))
      .filter((edge) => edge.to.trim().length > 0);

    // 単一無条件 edge は next に正規化する。
    if (!node.next?.trim() && edges.length === 1 && !edges[0].when && edges[0].default !== true && edges[0].order == null) {
      base.next = edges[0].to;
      return base;
    }

    if (node.next?.trim()) {
      base.next = node.next.trim();
    }
    if (edges.length > 0) {
      base.edges = edges;
    }
    return base;
  });

  const root: Record<string, unknown> = {
    version: document.version,
    workflow: {
      name: document.workflow.name
    },
    nodes
  };
  return stringify(root);
}
