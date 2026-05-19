import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, GraphDefinitionMeta } from "../graphs/types";
import type { ExecutionNodeDTO, NodeStatus, WorkflowView } from "./types";

/** 実行＋定義をマージしたグラフノード。 */
export type MergedGraphNode = {
  nodeId: string;
  /** ExecutionGraph のノード ID（差分ハイライト・ランタイム行と対応）。定義のみの IDLE 行では `nodeId` と同一の合成値。 */
  executionNodeId: string;
  /** 定義グラフ・API の状態名（実行ノードに stateName があればそれを優先） */
  stateName: string;
  nodeType: string;
  label: string;
  branch?: string;
  status: NodeStatus;
  attempt: number;
  /** ランタイム行がマージされたときのワーカー ID（定義のみの IDLE 行では null）。 */
  workerId: string | null;
  waitKey: string | null;
  canceledByExecution: boolean;
};

/** 実行＋定義をマージしたグラフ辺。 */
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

/** マージ済みグラフ全体。 */
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
    executionNodeId: nodeId,
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

/** 実行グラフと定義グラフをマージする。 */
export function mergeGraph(execution: WorkflowView, definition: GraphDefinition | null): MergedGraph {
  const byRuntimeId = new Map<string, ExecutionNodeDTO>();
  const byStateNameKey = new Map<string, ExecutionNodeDTO>();
  const stateNameByRuntimeId = new Map<string, string>();
  for (const n of execution.nodes) {
    byRuntimeId.set(n.executionNodeId, n);
    const trimmed = typeof n.stateName === "string" ? n.stateName.trim() : "";
    if (trimmed.length === 0) continue;
    byStateNameKey.set(trimmed, n);
    stateNameByRuntimeId.set(n.executionNodeId, trimmed);
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
        nodeId: n.executionNodeId,
        executionNodeId: n.executionNodeId,
        stateName: n.stateName ?? "",
        nodeType: n.nodeType,
        label: n.executionNodeId,
        status: n.status,
        attempt: n.attempt,
        workerId: n.workerId,
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
      executionNodeId: runtimeNode.executionNodeId,
      stateName: resolvedStateName,
      nodeType: defNode.nodeType,
      label: defNode.label ?? defNode.nodeId,
      branch: defNode.branch,
      status: runtimeNode.status,
      attempt: runtimeNode.attempt,
      workerId: runtimeNode.workerId,
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

