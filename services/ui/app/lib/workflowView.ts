import type {
  ExecutionNodeDTO,
  RuntimeGraphEdgeDTO,
  WorkflowDTO,
  WorkflowGraphDTO,
  WorkflowView
} from "./types";

/** C# WorkflowGraphDTO のノードを ExecutionNodeDTO に変換（v2）。GET /graph のノード ID は JSON の `nodeId` のみ（Core-API 永続スナップショットと一致）。 */
function graphNodeToExecutionNode(n: WorkflowGraphDTO["nodes"][0]): ExecutionNodeDTO {
  const executionNodeId =
    typeof n.nodeId === "string" && n.nodeId.length > 0 ? n.nodeId : "";
  const stateName = typeof n.stateName === "string" ? n.stateName : "";
  const nodeType = typeof n.nodeType === "string" ? n.nodeType : "";
  const fact = n.fact;
  const startedAt = typeof n.startedAt === "string" ? n.startedAt : undefined;
  const completedAt = typeof n.completedAt === "string" ? n.completedAt : null;
  const input = "input" in n ? n.input : undefined;
  const output = "output" in n ? n.output : undefined;
  const attempt = parseAttempt(n);
  const workerId = parseNullableString(n.workerId);
  const waitKey = parseNullableString(n.waitKey);
  const factText = toFactText(fact);
  const canceledByExecution = parseCanceledByExecution(n.canceledByExecution, factText);
  const conditionRouting = n.conditionRouting;

  const status = resolveNodeStatus(completedAt, factText);

  return {
    executionNodeId,
    stateName,
    nodeType,
    status,
    attempt,
    workerId,
    waitKey,
    canceledByExecution,
    startedAt,
    completedAt,
    input,
    output,
    conditionRouting
  };
}

function parseAttempt(node: WorkflowGraphDTO["nodes"][0]): number {
  return typeof node.attempt === "number" ? node.attempt : 1;
}

function parseNullableString(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

function toFactText(fact: unknown): string {
  return typeof fact === "string" ? fact.toLowerCase() : "";
}

function parseCanceledByExecution(value: unknown, factText: string): boolean {
  return typeof value === "boolean" ? value : factText.includes("cancel");
}

function resolveNodeStatus(completedAt: string | null, factText: string): ExecutionNodeDTO["status"] {
  if (completedAt == null) return "RUNNING";
  if (factText.includes("fail")) return "FAILED";
  if (factText.includes("cancel")) return "CANCELED";
  return "SUCCEEDED";
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
