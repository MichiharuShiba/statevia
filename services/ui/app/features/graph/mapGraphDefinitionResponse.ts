import type { GraphDefinition, GraphEdgeDef, GraphNodeDef, LayoutHints } from "../../graphs/types";

/** GET /v1/graphs/{graphId}（GraphDefinitionResponse）の緩い形。camelCase / PascalCase 両対応。 */
type ApiGraphNode = {
  nodeId?: string;
  NodeId?: string;
  nodeType?: string;
  NodeType?: string;
  label?: string;
  Label?: string;
  branch?: string;
  Branch?: string;
};

type ApiGraphEdge = {
  from?: string;
  From?: string;
  to?: string;
  To?: string;
  kind?: "normal" | "fork" | "join";
  Kind?: "normal" | "fork" | "join";
  edgeType?: GraphEdgeDef["edgeType"];
  EdgeType?: GraphEdgeDef["edgeType"];
  eventName?: string;
  EventName?: string;
  cancelReason?: string;
  CancelReason?: string;
  cancelCause?: string;
  CancelCause?: string;
};

type ApiGraphUi = {
  layout?: string;
  Layout?: string;
  positions?: Record<string, { x?: number; y?: number; X?: number; Y?: number }>;
  Positions?: Record<string, { x?: number; y?: number; X?: number; Y?: number }>;
};

type ApiGraphDefinitionResponse = {
  graphId?: string;
  GraphId?: string;
  nodes?: ApiGraphNode[];
  Nodes?: ApiGraphNode[];
  edges?: ApiGraphEdge[];
  Edges?: ApiGraphEdge[];
  ui?: ApiGraphUi;
  Ui?: ApiGraphUi;
  groups?: GraphDefinition["groups"];
  Groups?: GraphDefinition["groups"];
  layoutHints?: LayoutHints;
  LayoutHints?: LayoutHints;
};

function str(obj: Record<string, unknown>, camel: string, pascal: string): string {
  const v = obj[camel] ?? obj[pascal];
  return typeof v === "string" ? v : "";
}

function mapNode(n: ApiGraphNode): GraphNodeDef {
  const o = n as Record<string, unknown>;
  return {
    nodeId: str(o, "nodeId", "NodeId"),
    nodeType: str(o, "nodeType", "NodeType") || "Task",
    label: str(o, "label", "Label") || undefined,
    branch: str(o, "branch", "Branch") || undefined
  };
}

function mapEdge(e: ApiGraphEdge): GraphEdgeDef {
  const o = e as Record<string, unknown>;
  return {
    from: str(o, "from", "From"),
    to: str(o, "to", "To"),
    kind: (e.kind ?? e.Kind) || undefined,
    edgeType: (e.edgeType ?? e.EdgeType) || undefined,
    eventName: str(o, "eventName", "EventName") || undefined,
    cancelReason: str(o, "cancelReason", "CancelReason") || undefined,
    cancelCause: str(o, "cancelCause", "CancelCause") || undefined
  };
}

/**
 * Core-API の GraphDefinitionResponse を UI の GraphDefinition に変換する。
 * 不正・空の場合は null。
 */
export function mapGraphDefinitionResponse(raw: unknown, fallbackGraphId: string): GraphDefinition | null {
  if (raw === null || typeof raw !== "object") return null;
  const o = raw as ApiGraphDefinitionResponse;
  const graphId = o.graphId ?? o.GraphId ?? fallbackGraphId;
  const nodesRaw = o.nodes ?? o.Nodes ?? [];
  const edgesRaw = o.edges ?? o.Edges ?? [];
  if (!Array.isArray(nodesRaw) || nodesRaw.length === 0) return null;

  const nodes = nodesRaw.map(mapNode).filter((n) => n.nodeId.length > 0);
  if (nodes.length === 0) return null;

  const edges = Array.isArray(edgesRaw) ? edgesRaw.map(mapEdge).filter((e) => e.from && e.to) : [];

  const groups = o.groups ?? o.Groups;
  let layoutHints = o.layoutHints ?? o.LayoutHints;
  const ui = o.ui ?? o.Ui;
  if (!layoutHints && ui) {
    const positions = ui.positions ?? ui.Positions;
    const hasPositions = positions != null && typeof positions === "object";
    const hasLayout = Boolean(ui.layout ?? ui.Layout);
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
