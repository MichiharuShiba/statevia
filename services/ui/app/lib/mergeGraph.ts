import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, GraphDefinitionMeta } from "../graphs/types";
import type { ExecutionNodeDTO, NodeStatus, WorkflowView } from "./types";

export type MergedGraphNode = {
  nodeId: string;
  /** 定義グラフ・API の状態名（実行ノードに stateName があればそれを優先） */
  stateName: string;
  nodeType: string;
  label: string;
  branch?: string;
  status: NodeStatus;
  attempt: number;
  waitKey: string | null;
  canceledByExecution: boolean;
};

export type MergedGraphEdge = {
  id: string;
  from: string;
  to: string;
  kind?: "normal" | "fork" | "join";
  edgeType?: "Next" | "Resume" | "Cancel";
  eventName?: string;
  cancelReason?: string;
  cancelCause?: string;
  traversed?: boolean;
};

export type MergedGraph = {
  graphId: string;
  nodes: MergedGraphNode[];
  edges: MergedGraphEdge[];
  groups?: GraphGroupDef[];
  meta?: GraphDefinitionMeta;
  isDefinitionBased: boolean;
};

/** 定義のみ存在するノード用。`nodeId` は定義上のノードキー、`stateName` はワークフロー状態名。 */
function asIdleNode(nodeId: string, stateName: string, nodeType: string): ExecutionNodeDTO {
  return {
    nodeId,
    stateName,
    nodeType,
    status: "IDLE",
    attempt: 0,
    workerId: null,
    waitKey: null,
    canceledByExecution: false
  };
}

function toEdge(edge: GraphEdgeDef, index: number): MergedGraphEdge {
  return {
    id: `e-${edge.from}-${edge.to}-${index}`,
    from: edge.from,
    to: edge.to,
    kind: edge.kind,
    edgeType: edge.edgeType,
    eventName: edge.eventName,
    cancelReason: edge.cancelReason,
    cancelCause: edge.cancelCause,
    traversed: false
  };
}

export function mergeGraph(execution: WorkflowView, definition: GraphDefinition | null): MergedGraph {
  const byRuntimeId = new Map<string, ExecutionNodeDTO>();
  const byStateNameKey = new Map<string, ExecutionNodeDTO>();
  const stateNameByRuntimeId = new Map<string, string>();
  for (const n of execution.nodes) {
    byRuntimeId.set(n.nodeId, n);
    const trimmed = typeof n.stateName === "string" ? n.stateName.trim() : "";
    if (trimmed.length === 0) continue;
    byStateNameKey.set(trimmed, n);
    stateNameByRuntimeId.set(n.nodeId, trimmed);
  }

  const traversedEdgeKeys = new Set(
    (execution.runtimeEdges ?? []).flatMap((edge) => {
      const directKey = `${edge.from}->${edge.to}`;
      const fromState = stateNameByRuntimeId.get(edge.from);
      const toState = stateNameByRuntimeId.get(edge.to);
      if (fromState === undefined || toState === undefined) return [directKey];
      return [directKey, `${fromState}->${toState}`];
    })
  );
  if (!definition) {
    return {
      graphId: execution.graphId,
      nodes: execution.nodes.map((n) => ({
        nodeId: n.nodeId,
        stateName: n.stateName ?? "",
        nodeType: n.nodeType,
        label: n.nodeId,
        status: n.status,
        attempt: n.attempt,
        waitKey: n.waitKey,
        canceledByExecution: n.canceledByExecution
      })),
      edges: [],
      groups: [],
      meta: { direction: "TB" },
      isDefinitionBased: false
    };
  }

  const nodes = definition.nodes.map((defNode) => {
    const definitionStateName =
      typeof defNode.stateName === "string" && defNode.stateName.trim().length > 0
        ? defNode.stateName.trim()
        : defNode.nodeId;

    const runtimeNode =
      byRuntimeId.get(defNode.nodeId) ??
      byStateNameKey.get(definitionStateName) ??
      asIdleNode(defNode.nodeId, definitionStateName, defNode.nodeType);

    const resolvedStateName =
      typeof runtimeNode.stateName === "string" && runtimeNode.stateName.trim().length > 0
        ? runtimeNode.stateName.trim()
        : definitionStateName;

    return {
      nodeId: defNode.nodeId,
      stateName: resolvedStateName,
      nodeType: defNode.nodeType,
      label: defNode.label ?? defNode.nodeId,
      branch: defNode.branch,
      status: runtimeNode.status,
      attempt: runtimeNode.attempt,
      waitKey: runtimeNode.waitKey,
      canceledByExecution: runtimeNode.canceledByExecution
    };
  });

  return {
    graphId: definition.graphId,
    nodes,
    edges: definition.edges.map((defEdge, index) => toEdge(defEdge, index)).map((edge) => ({
      ...edge,
      traversed: traversedEdgeKeys.has(`${edge.from}->${edge.to}`)
    })),
    groups: definition.groups ?? [],
    meta: definition.meta,
    isDefinitionBased: true
  };
}

