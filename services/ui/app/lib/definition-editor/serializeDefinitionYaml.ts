import { stringify } from "yaml";
import type { DefinitionGraphDocument, DefinitionGraphEdge } from "./types";

function isNonEmptyRecord(value: Record<string, unknown>): boolean {
  return Object.keys(value).length > 0;
}

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
    if (node.type === "join" && node.mode === "all") {
      base.mode = "all";
    }
    if (node.input !== undefined) {
      const emit =
        typeof node.input === "string" || isNonEmptyRecord(node.input);
      if (emit) {
        base.input = node.input;
      }
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

  const workflowOut: Record<string, unknown> = {
    name: document.workflow.name
  };
  if (document.workflow.id?.trim()) {
    workflowOut.id = document.workflow.id.trim();
  }
  if (document.workflow.description?.trim()) {
    workflowOut.description = document.workflow.description.trim();
  }

  const root: Record<string, unknown> = {
    version: document.version,
    workflow: workflowOut,
    nodes
  };
  return stringify(root);
}
