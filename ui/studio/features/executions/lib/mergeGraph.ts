import type { GraphDefinition, GraphEdgeDef, GraphGroupDef, GraphDefinitionMeta } from "@/features/executions/graphs/types";
import type { ExecutionNodeDTO, NodeStatus, ExecutionView } from "../types";

/** 実行＋定義をマージしたグラフノード。 */
export type MergedGraphNode = {
  /** 定義グラフ上のノード名（GraphNodeDef.nodeName）。エッジ・レイアウト・グループのキー。 */
  name: string;
  /** ExecutionGraph のノード ID（差分ハイライト・ランタイム行と対応）。定義のみの IDLE 行では `name` と同一の合成値。 */
  executionNodeId: string;
  /** 定義グラフ・API の状態名（実行ノードに nodeName があればそれを優先） */
  nodeName: string;
  nodeType: string;
  label: string;
  branch?: string;
  status: NodeStatus;
  attempt: number;
  /** ランタイム行がマージされたときのワーカー ID（定義のみの IDLE 行では null）。 */
  workerId: string | null;
  waitKey: string | null;
  /** Wait が受付可能なイベント名一覧（任意）。 */
  allowedEvents?: string[] | null;
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

/** 定義のみ存在するノード用。`name` は定義上のノード名、`nodeName` はワークフロー状態名（通常は同一値）。 */
function asIdleNode(name: string, nodeName: string, nodeType: string): ExecutionNodeDTO {
  return {
    executionNodeId: name,
    nodeName,
    nodeType,
    status: "IDLE",
    attempt: 0,
    workerId: null,
    waitKey: null,
    allowedEvents: null,
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
export function mergeGraph(execution: ExecutionView, definition: GraphDefinition | null): MergedGraph {
  const byRuntimeId = new Map<string, ExecutionNodeDTO>();
  const byNodeNameKey = new Map<string, ExecutionNodeDTO>();
  const nodeNameByRuntimeId = new Map<string, string>();
  for (const n of execution.nodes) {
    byRuntimeId.set(n.executionNodeId, n);
    const trimmed = typeof n.nodeName === "string" ? n.nodeName.trim() : "";
    if (trimmed.length === 0) continue;
    byNodeNameKey.set(trimmed, n);
    nodeNameByRuntimeId.set(n.executionNodeId, trimmed);
  }

  const traversedEdgeKeys = new Set(
    (execution.runtimeEdges ?? []).flatMap((edge) => {
      const directKey = `${edge.from}->${edge.to}`;
      const fromState = nodeNameByRuntimeId.get(edge.from);
      const toState = nodeNameByRuntimeId.get(edge.to);
      if (fromState === undefined || toState === undefined) return [directKey];
      return [directKey, `${fromState}->${toState}`];
    })
  );
  if (!definition) {
    return {
      graphId: execution.graphId,
      nodes: execution.nodes.map((n) => ({
        name: n.executionNodeId,
        executionNodeId: n.executionNodeId,
        nodeName: n.nodeName ?? "",
        nodeType: n.nodeType,
        label: n.executionNodeId,
        status: n.status,
        attempt: n.attempt,
        workerId: n.workerId,
        waitKey: n.waitKey,
        allowedEvents: n.allowedEvents ?? null,
        canceledByExecution: n.canceledByExecution
      })),
      edges: [],
      groups: [],
      meta: { direction: "TB" },
      isDefinitionBased: false
    };
  }

  const nodes = definition.nodes.map((defNode) => {
    const definitionNodeName = defNode.nodeName.trim();

    // 定義グラフのノード名（＝ワークフロー状態名）で実行ノードを突き合わせる。
    // 突き合わせ不可（旧データ等）の際のみ実行ノード ID 直接一致にフォールバックする。
    const runtimeNode =
      byNodeNameKey.get(definitionNodeName) ??
      byRuntimeId.get(defNode.nodeName) ??
      asIdleNode(defNode.nodeName, definitionNodeName, defNode.nodeType);

    const resolvedNodeName =
      typeof runtimeNode.nodeName === "string" && runtimeNode.nodeName.trim().length > 0
        ? runtimeNode.nodeName.trim()
        : definitionNodeName;

    return {
      name: defNode.nodeName,
      executionNodeId: runtimeNode.executionNodeId,
      nodeName: resolvedNodeName,
      nodeType: defNode.nodeType,
      label: defNode.label ?? defNode.nodeName,
      branch: defNode.branch,
      status: runtimeNode.status,
      attempt: runtimeNode.attempt,
      workerId: runtimeNode.workerId,
      waitKey: runtimeNode.waitKey,
      allowedEvents: runtimeNode.allowedEvents ?? null,
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
