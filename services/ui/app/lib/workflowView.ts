import type {
  ExecutionNodeDTO,
  WorkflowDTO,
  WorkflowGraphDTO,
  WorkflowView
} from "./types";

function pick<T extends Record<string, unknown>, K extends string>(
  obj: T,
  ...keys: [K, string]
): string {
  const v = obj[keys[0]] ?? obj[keys[1]];
  return typeof v === "string" ? v : "";
}

/** C# WorkflowGraphDTO のノードを ExecutionNodeDTO に変換（v2）。camelCase / PascalCase 両対応。 */
function graphNodeToExecutionNode(n: WorkflowGraphDTO["nodes"][0]): ExecutionNodeDTO {
  let status: ExecutionNodeDTO["status"] = "RUNNING";
  const completedAt = n.completedAt ?? n.CompletedAt;
  const fact = String(n.fact ?? n.Fact ?? "").toLowerCase();
  if (completedAt != null) {
    if (fact.includes("fail")) status = "FAILED";
    else if (fact.includes("cancel")) status = "CANCELED";
    else status = "SUCCEEDED";
  }
  return {
    nodeId: pick(n as Record<string, unknown>, "nodeId", "NodeId"),
    nodeType: pick(n as Record<string, unknown>, "stateName", "StateName"),
    status,
    attempt: 0,
    workerId: null,
    waitKey: null,
    canceledByExecution: false
  };
}

/** WorkflowDTO と graph から WorkflowView を組み立てる（v2）。 */
export function buildWorkflowView(
  workflow: WorkflowDTO,
  graph: WorkflowGraphDTO | null
): WorkflowView {
  const nodes: ExecutionNodeDTO[] = graph?.nodes?.map(graphNodeToExecutionNode) ?? [];
  return {
    ...workflow,
    graphId: workflow.resourceId,
    nodes
  };
}
