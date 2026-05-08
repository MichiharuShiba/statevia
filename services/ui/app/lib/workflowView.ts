import type {
  ExecutionNodeDTO,
  RuntimeGraphEdgeDTO,
  WorkflowDTO,
  WorkflowGraphDTO,
  WorkflowView
} from "./types";

/** C# WorkflowGraphDTO のノードを ExecutionNodeDTO に変換（v2）。Core-API の camelCase JSON を前提。 */
function graphNodeToExecutionNode(n: WorkflowGraphDTO["nodes"][0]): ExecutionNodeDTO {
  const nodeId = typeof n.nodeId === "string" ? n.nodeId : "";
  const stateName = typeof n.stateName === "string" ? n.stateName : "";
  const fact = n.fact;
  const startedAt = typeof n.startedAt === "string" ? n.startedAt : undefined;
  const completedAt = typeof n.completedAt === "string" ? n.completedAt : null;
  const output = "output" in n ? n.output : undefined;
  const conditionRouting = n.conditionRouting;

  let status: ExecutionNodeDTO["status"] = "RUNNING";
  const factText = String(fact ?? "").toLowerCase();
  if (completedAt != null) {
    if (factText.includes("fail")) status = "FAILED";
    else if (factText.includes("cancel")) status = "CANCELED";
    else status = "SUCCEEDED";
  }

  return {
    nodeId,
    // 現在の /graph ノードは state type（Start/Task/...）を返さないため、
    // ひとまず stateName を nodeType へ入れて扱う。
    // 定義グラフがある画面では mergeGraph 側で definition.nodeType が使われる。
    nodeType: stateName,
    status,
    // NOTE:
    // attempt / workerId / waitKey / canceledByExecution は /graph 応答に含まれないため、
    // API/Engine 側で公開されるまでは暫定値を設定する。
    attempt: 0,
    workerId: null,
    waitKey: null,
    canceledByExecution: false,
    startedAt,
    completedAt,
    output,
    conditionRouting
  };
}

function graphEdgeToRuntimeEdge(edge: WorkflowGraphDTO["edges"][0]): RuntimeGraphEdgeDTO | null {
  const from = typeof edge.from === "string" ? edge.from : null;
  const to = typeof edge.to === "string" ? edge.to : null;
  if (!from || !to) return null;
  let type: number | undefined;
  if (typeof edge.type === "number") {
    type = edge.type;
  }
  return { from, to, type };
}

/** WorkflowDTO と graph から WorkflowView を組み立てる（v2）。 */
export function buildWorkflowView(
  workflow: WorkflowDTO,
  graph: WorkflowGraphDTO | null
): WorkflowView {
  const nodes: ExecutionNodeDTO[] = graph?.nodes?.map(graphNodeToExecutionNode) ?? [];
  const runtimeEdges: RuntimeGraphEdgeDTO[] = (graph?.edges ?? [])
    .map(graphEdgeToRuntimeEdge)
    .filter((edge): edge is RuntimeGraphEdgeDTO => edge != null);
  return {
    ...workflow,
    graphId: workflow.graphId,
    nodes,
    runtimeEdges
  };
}
