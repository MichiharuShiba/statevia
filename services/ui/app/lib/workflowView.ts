import type {
  ExecutionNodeDTO,
  WorkflowDTO,
  WorkflowGraphDTO,
  WorkflowView
} from "./types";

/** C# WorkflowGraphDTO のノードを ExecutionNodeDTO に変換（v2）。Core-API の camelCase JSON を前提。 */
function graphNodeToExecutionNode(n: WorkflowGraphDTO["nodes"][0]): ExecutionNodeDTO {
  let status: ExecutionNodeDTO["status"] = "RUNNING";
  const completedAt = n.completedAt;
  const fact = String(n.fact ?? "").toLowerCase();
  if (completedAt != null) {
    if (fact.includes("fail")) status = "FAILED";
    else if (fact.includes("cancel")) status = "CANCELED";
    else status = "SUCCEEDED";
  }
  return {
    nodeId: typeof n.nodeId === "string" ? n.nodeId : "",
    nodeType: typeof n.stateName === "string" ? n.stateName : "",
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
