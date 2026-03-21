import type { GraphDefinition, GraphEdgeDef, GraphNodeDef, LayoutHints } from "../../graphs/types";

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

type ApiGraphUi = {
  layout?: string;
  positions?: Record<string, { x?: number; y?: number }>;
};

type ApiGraphDefinitionResponse = {
  graphId?: string;
  nodes?: ApiGraphNode[];
  edges?: ApiGraphEdge[];
  ui?: ApiGraphUi;
  groups?: GraphDefinition["groups"];
  layoutHints?: LayoutHints;
};

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
  let layoutHints = o.layoutHints;
  const ui = o.ui;
  if (!layoutHints && ui) {
    const positions = ui.positions;
    const hasPositions = positions != null && typeof positions === "object";
    const hasLayout = typeof ui.layout === "string" && ui.layout.length > 0;
    if (hasPositions || hasLayout) {
      layoutHints = { direction: "LR" };
    }
  }

  return {
    graphId,
    nodes,
    edges,
    groups: Array.isArray(groups) ? groups : undefined,
    layoutHints
  };
}
