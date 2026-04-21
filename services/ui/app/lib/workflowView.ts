import type {
  ExecutionNodeDTO,
  WorkflowDTO,
  WorkflowGraphDTO,
  WorkflowView
} from "./types";

/** C# WorkflowGraphDTO のノードを ExecutionNodeDTO に変換（v2）。Core-API の camelCase JSON を前提。 */
function graphNodeToExecutionNode(n: WorkflowGraphDTO["nodes"][0]): ExecutionNodeDTO {
  const nodeId =
    (typeof n.nodeId === "string" ? n.nodeId : null) ??
    (typeof (n as Record<string, unknown>).NodeId === "string" ? ((n as Record<string, unknown>).NodeId as string) : "");
  const stateName =
    (typeof n.stateName === "string" ? n.stateName : null) ??
    (typeof (n as Record<string, unknown>).StateName === "string"
      ? ((n as Record<string, unknown>).StateName as string)
      : "");
  const completedAt =
    n.completedAt ??
    ((n as Record<string, unknown>).CompletedAt as string | null | undefined);
  const fact =
    n.fact ??
    ((n as Record<string, unknown>).Fact as string | null | undefined);
  const conditionRouting =
    n.conditionRouting ??
    (n as Record<string, unknown>).ConditionRouting;

  let status: ExecutionNodeDTO["status"] = "RUNNING";
  const factText = String(fact ?? "").toLowerCase();
  if (completedAt != null) {
    if (factText.includes("fail")) status = "FAILED";
    else if (factText.includes("cancel")) status = "CANCELED";
    else status = "SUCCEEDED";
  }
  return {
    nodeId,
    nodeType: stateName,
    status,
    attempt: 0,
    workerId: null,
    waitKey: null,
    canceledByExecution: false,
    conditionRouting
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
