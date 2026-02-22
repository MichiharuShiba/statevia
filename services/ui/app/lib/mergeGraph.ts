import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, LayoutHints } from "../graphs/types";
import type { ExecutionDTO, ExecutionNodeDTO, NodeStatus } from "./types";

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
};

export type MergedGraph = {
  graphId: string;
  nodes: MergedGraphNode[];
  edges: MergedGraphEdge[];
  groups?: GraphGroupDef[];
  layoutHints?: LayoutHints;
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
    kind: edge.kind
  };
}

export function mergeGraph(execution: ExecutionDTO, definition: GraphDefinition | null): MergedGraph {
  const nodeById = new Map(execution.nodes.map((n) => [n.nodeId, n] as const));
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
      layoutHints: { direction: "LR" },
      isDefinitionBased: false
    };
  }

  const nodes = definition.nodes.map((defNode) => {
    const runtimeNode = nodeById.get(defNode.nodeId) ?? asIdleNode(defNode.nodeId, defNode.nodeType);
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
    edges: definition.edges.map(toEdge),
    groups: definition.groups ?? [],
    layoutHints: definition.layoutHints,
    isDefinitionBased: true
  };
}

