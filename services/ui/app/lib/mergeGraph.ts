import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, GraphDefinitionMeta } from "../graphs/types";
import type { ExecutionNodeDTO, NodeStatus, WorkflowView } from "./types";

export type MergedGraphNode = {
  nodeId: string;
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

function asIdleNode(nodeId: string, nodeType: string): ExecutionNodeDTO {
  return {
    nodeId,
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
  const nodeById = new Map(execution.nodes.map((n) => [n.nodeId, n] as const));
  const nodeByStateName = new Map(execution.nodes.map((n) => [n.nodeType, n] as const));
  const runtimeStateByNodeId = new Map(execution.nodes.map((n) => [n.nodeId, n.nodeType] as const));
  const traversedEdgeKeys = new Set(
    (execution.runtimeEdges ?? []).flatMap((edge) => {
      const directKey = `${edge.from}->${edge.to}`;
      const fromState = runtimeStateByNodeId.get(edge.from);
      const toState = runtimeStateByNodeId.get(edge.to);
      if (!fromState || !toState) return [directKey];
      return [directKey, `${fromState}->${toState}`];
    })
  );
  if (!definition) {
    return {
      graphId: execution.graphId,
      nodes: execution.nodes.map((n) => ({
        nodeId: n.nodeId,
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
    const runtimeNode =
      nodeById.get(defNode.nodeId) ??
      nodeByStateName.get(defNode.nodeId) ??
      asIdleNode(defNode.nodeId, defNode.nodeType);
    return {
      nodeId: defNode.nodeId,
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

