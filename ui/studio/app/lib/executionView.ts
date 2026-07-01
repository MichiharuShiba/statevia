import type {
  ExecutionDTO,
  ExecutionGraphDTO,
  ExecutionNodeDTO,
  ExecutionView,
  RuntimeGraphEdgeDTO
} from "./types";

/** C# ExecutionGraphResponse のノードを ExecutionNodeDTO に変換（v2）。GET /graph のノード ID は JSON の `nodeId` のみ（Core-API 永続スナップショットと一致）。 */
function graphNodeToExecutionNode(n: ExecutionGraphDTO["nodes"][0]): ExecutionNodeDTO {
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

function parseAttempt(node: ExecutionGraphDTO["nodes"][0]): number {
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

function graphEdgeToRuntimeEdge(edge: ExecutionGraphDTO["edges"][0]): RuntimeGraphEdgeDTO | null {
  const from = typeof edge.from === "string" ? edge.from : null;
  const to = typeof edge.to === "string" ? edge.to : null;
  if (!from || !to) return null;
  let type: number | undefined;
  if (typeof edge.type === "number") {
    type = edge.type;
  }
  return { from, to, type };
}

/** ExecutionDTO と graph から ExecutionView を組み立てる（v2）。 */
export function buildExecutionView(
  execution: ExecutionDTO,
  graph: ExecutionGraphDTO | null
): ExecutionView {
  const nodes: ExecutionNodeDTO[] = graph?.nodes?.map(graphNodeToExecutionNode) ?? [];
  const runtimeEdges: RuntimeGraphEdgeDTO[] = (graph?.edges ?? [])
    .map(graphEdgeToRuntimeEdge)
    .filter((edge): edge is RuntimeGraphEdgeDTO => edge != null);
  return {
    ...execution,
    graphId: execution.graphId,
    nodes,
    runtimeEdges
  };
}
