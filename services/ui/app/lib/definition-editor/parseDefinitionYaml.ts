import { parseDocument } from "yaml";
import { NODE_TYPES } from "./types";
import type {
  DefinitionGraphDocument,
  DefinitionGraphEdge,
  DefinitionGraphMeta,
  DefinitionGraphNode,
  EdgeCondition,
  NodeType,
  ParseDefinitionYamlResult
} from "./types";

export type ParseDefinitionYamlMessageOptions = {
  rootObjectRequired: () => string;
  nodesArrayRequired: () => string;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isNodeType(value: string): value is NodeType {
  return NODE_TYPES.includes(value as (typeof NODE_TYPES)[number]);
}

function parseNodeType(value: unknown): NodeType | null {
  if (typeof value !== "string") {
    return null;
  }
  const normalized = value.trim();
  return isNodeType(normalized) ? normalized : null;
}

function parseCondition(value: unknown): EdgeCondition | undefined {
  if (!isRecord(value)) {
    return undefined;
  }
  const path = typeof value.path === "string" ? value.path : "";
  const op = typeof value.op === "string" ? value.op : "";
  return {
    path,
    op,
    value: value.value
  };
}

function parseEdge(value: unknown): DefinitionGraphEdge | null {
  if (!isRecord(value)) {
    return null;
  }
  const to = typeof value.to === "string" ? value.to : "";
  const order = typeof value.order === "number" ? value.order : undefined;
  const isDefault = value.default === true;
  return {
    to,
    when: parseCondition(value.when),
    order,
    default: isDefault || undefined
  };
}

function parseVersion(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (!trimmed) {
      return 1;
    }
    const parsed = Number.parseInt(trimmed, 10);
    return Number.isFinite(parsed) ? parsed : 1;
  }
  return 1;
}

function parseNode(value: unknown): DefinitionGraphNode | null {
  if (!isRecord(value)) {
    return null;
  }
  const id = typeof value.id === "string" ? value.id : "";
  const type = parseNodeType(value.type);
  if (!id || type == null) {
    return null;
  }
  const node: DefinitionGraphNode = {
    id,
    type
  };
  if (typeof value.action === "string") {
    node.action = value.action;
  }
  if (typeof value.event === "string") {
    node.event = value.event;
  }
  if (typeof value.next === "string") {
    node.next = value.next;
  }
  if (Array.isArray(value.branches)) {
    node.branches = value.branches.filter((entry): entry is string => typeof entry === "string");
  }
  if (Array.isArray(value.edges)) {
    node.edges = value.edges
      .map((edge) => parseEdge(edge))
      .filter((edge): edge is DefinitionGraphEdge => edge !== null);
  }
  if (isRecord(value.input)) {
    node.input = value.input;
  }
  if (type === "join") {
    node.mode = "all";
  }
  return node;
}

/**
 * YAML テキストを DefinitionGraphDocument へ変換する。
 * ここでは構文・基本形のみを扱い、ドメイン整合性検証は呼び出し側で行う。
 */
export function parseDefinitionYaml(
  yamlText: string,
  options: ParseDefinitionYamlMessageOptions
): ParseDefinitionYamlResult {
  const parsed = parseDocument(yamlText, { prettyErrors: false });
  if (parsed.errors.length > 0) {
    return {
      document: null,
      diagnostics: parsed.errors.map((error) => error.message)
    };
  }
  const root = parsed.toJS() as unknown;
  if (!isRecord(root)) {
    return {
      document: null,
      diagnostics: [options.rootObjectRequired()]
    };
  }

  const workflow = isRecord(root.workflow) ? root.workflow : {};
  const workflowName =
    typeof workflow.name === "string" && workflow.name.trim().length > 0
      ? workflow.name.trim()
      : "Unnamed";

  if (!Array.isArray(root.nodes)) {
    return {
      document: null,
      diagnostics: [options.nodesArrayRequired()]
    };
  }

  const nodes = root.nodes
    .map((entry) => parseNode(entry))
    .filter((entry): entry is DefinitionGraphNode => entry !== null);

  const version = parseVersion(root.version);

  function parseLayoutRecord(raw: unknown): Record<string, { x: number; y: number }> | null {
    if (!isRecord(raw)) {
      return null;
    }
    const gp: Record<string, { x: number; y: number }> = {};
    for (const [id, val] of Object.entries(raw)) {
      if (!id.trim() || !isRecord(val)) {
        continue;
      }
      const x = typeof val.x === "number" && Number.isFinite(val.x) ? val.x : null;
      const y = typeof val.y === "number" && Number.isFinite(val.y) ? val.y : null;
      if (x != null && y != null) {
        gp[id] = { x, y };
      }
    }
    return Object.keys(gp).length > 0 ? gp : null;
  }

  const metaLayout = isRecord(root.meta) ? parseLayoutRecord((root.meta as DefinitionGraphMeta).layout) : null;
  const legacyGraphPositions = parseLayoutRecord(root.graphPositions);
  const mergedLayout: Record<string, { x: number; y: number }> = {};
  if (legacyGraphPositions) {
    Object.assign(mergedLayout, legacyGraphPositions);
  }
  if (metaLayout) {
    Object.assign(mergedLayout, metaLayout);
  }

  let meta: DefinitionGraphMeta | undefined;
  if (Object.keys(mergedLayout).length > 0) {
    meta = { layout: mergedLayout };
  }

  const document: DefinitionGraphDocument = {
    version,
    workflow: { name: workflowName },
    nodes,
    ...(meta ? { meta } : {})
  };

  return {
    document,
    diagnostics: []
  };
}
