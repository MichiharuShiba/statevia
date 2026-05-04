import type { GraphDefinition, GraphDefinitionMeta, GraphEdgeDef, GraphNodeDef } from "../../graphs/types";

/** GET /v1/graphs/{graphId}（GraphDefinitionResponse）の緩い形。Core-API の camelCase JSON を前提。 */
type ApiGraphNode = {
  nodeId?: string;
  nodeType?: string;
  label?: string;
  branch?: string;
};

type ApiGraphEdge = {
  from?: string;
  to?: string;
  kind?: "normal" | "fork" | "join";
  edgeType?: GraphEdgeDef["edgeType"];
  eventName?: string;
  cancelReason?: string;
  cancelCause?: string;
};

type ApiGraphDefinitionResponse = {
  graphId?: string;
  nodes?: ApiGraphNode[];
  edges?: ApiGraphEdge[];
  meta?: unknown;
  groups?: GraphDefinition["groups"];
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === "object" && !Array.isArray(v);
}

function parsePosition(pos: unknown): { x: number; y: number } | null {
  if (!isRecord(pos)) return null;
  const x = pos.x;
  const y = pos.y;
  if (typeof x !== "number" || typeof y !== "number" || !Number.isFinite(x) || !Number.isFinite(y)) return null;
  return { x, y };
}

function parseMetaLayout(rawMeta: unknown): GraphDefinitionMeta | undefined {
  if (!isRecord(rawMeta)) return undefined;
  const layoutRaw = rawMeta.layout;
  if (!isRecord(layoutRaw)) return undefined;
  const layout: Record<string, { x: number; y: number }> = {};
  for (const [nodeId, pos] of Object.entries(layoutRaw)) {
    const p = parsePosition(pos);
    if (p) layout[nodeId] = p;
  }
  return Object.keys(layout).length > 0 ? { layout } : undefined;
}

function mapNode(n: ApiGraphNode): GraphNodeDef {
  return {
    nodeId: typeof n.nodeId === "string" ? n.nodeId : "",
    nodeType: typeof n.nodeType === "string" && n.nodeType.length > 0 ? n.nodeType : "Task",
    label: typeof n.label === "string" && n.label.length > 0 ? n.label : undefined,
    branch: typeof n.branch === "string" && n.branch.length > 0 ? n.branch : undefined
  };
}

function mapEdge(e: ApiGraphEdge): GraphEdgeDef {
  return {
    from: typeof e.from === "string" ? e.from : "",
    to: typeof e.to === "string" ? e.to : "",
    kind: e.kind,
    edgeType: e.edgeType,
    eventName: typeof e.eventName === "string" && e.eventName.length > 0 ? e.eventName : undefined,
    cancelReason: typeof e.cancelReason === "string" && e.cancelReason.length > 0 ? e.cancelReason : undefined,
    cancelCause: typeof e.cancelCause === "string" && e.cancelCause.length > 0 ? e.cancelCause : undefined
  };
}

/**
 * Core-API の GraphDefinitionResponse を UI の GraphDefinition に変換する。
 * 保存済み座標は `meta.layout.<nodeId>.{x,y}` を前提とする（API が返さない場合もある）。
 * 不正・空の場合は null。
 */
export function mapGraphDefinitionResponse(raw: unknown, fallbackGraphId: string): GraphDefinition | null {
  if (raw === null || typeof raw !== "object") return null;
  const o = raw as ApiGraphDefinitionResponse;
  const graphId = typeof o.graphId === "string" && o.graphId.length > 0 ? o.graphId : fallbackGraphId;
  const nodesRaw = Array.isArray(o.nodes) ? o.nodes : [];
  const edgesRaw = Array.isArray(o.edges) ? o.edges : [];
  if (nodesRaw.length === 0) return null;

  const nodes = nodesRaw.map(mapNode).filter((n) => n.nodeId.length > 0);
  if (nodes.length === 0) return null;

  const edges = edgesRaw.map(mapEdge).filter((e) => e.from && e.to);

  const groups = o.groups;
  const meta = parseMetaLayout(o.meta);

  return {
    graphId,
    nodes,
    edges,
    groups: Array.isArray(groups) ? groups : undefined,
    ...(meta ? { meta } : {})
  };
}
