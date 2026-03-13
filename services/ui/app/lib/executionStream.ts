import type {
  ExecutionNodeDTO,
  ExecutionStreamEvent,
  NodeStatus,
  WorkflowStatus,
  WorkflowView
} from "./types";

function normalizeToWorkflowStatus(value: string): WorkflowStatus {
  const normalized = value.trim().toUpperCase();
  if (normalized === "CANCELLED" || normalized === "CANCELED") return "Cancelled";
  if (normalized === "COMPLETED") return "Completed";
  if (normalized === "FAILED") return "Failed";
  return "Running";
}

function normalizeNodeStatus(value: string): NodeStatus | null {
  const normalized = value.trim().toUpperCase();
  if (normalized === "CANCELLED") return "CANCELED";
  const allowed = new Set<NodeStatus>(["IDLE", "READY", "RUNNING", "WAITING", "SUCCEEDED", "FAILED", "CANCELED"]);
  return allowed.has(normalized as NodeStatus) ? (normalized as NodeStatus) : null;
}

function upsertNode(nodes: ExecutionNodeDTO[], incoming: Partial<ExecutionNodeDTO> & { nodeId: string }): ExecutionNodeDTO[] {
  const index = nodes.findIndex((node) => node.nodeId === incoming.nodeId);
  if (index < 0) {
    return [
      ...nodes,
      {
        nodeId: incoming.nodeId,
        nodeType: incoming.nodeType ?? "Unknown",
        status: incoming.status ?? "IDLE",
        attempt: incoming.attempt ?? 0,
        workerId: incoming.workerId ?? null,
        waitKey: incoming.waitKey ?? null,
        canceledByExecution: incoming.canceledByExecution ?? false,
        error: incoming.error ?? undefined,
        cancelReason: incoming.cancelReason ?? undefined
      }
    ];
  }

  const target = nodes[index];
  const nextNode: ExecutionNodeDTO = {
    ...target,
    ...incoming,
    nodeId: target.nodeId
  };

  return [...nodes.slice(0, index), nextNode, ...nodes.slice(index + 1)];
}

function applyGraphUpdated(execution: WorkflowView, event: Extract<ExecutionStreamEvent, { type: "GraphUpdated" }>): WorkflowView {
  const patchNodes = event.patch.nodes ?? [];
  const nextNodes = patchNodes.reduce((acc, patchNode) => {
    const normalizedStatus = patchNode.status ? normalizeNodeStatus(patchNode.status) : null;
    return upsertNode(acc, {
      nodeId: patchNode.nodeId,
      status: normalizedStatus ?? undefined,
      attempt: patchNode.attempt,
      waitKey: patchNode.waitKey,
      canceledByExecution: patchNode.canceledByExecution,
      error: patchNode.error,
      cancelReason: patchNode.cancelReason
    });
  }, execution.nodes);

  return { ...execution, nodes: nextNodes };
}

function applyStatusChanged(
  execution: WorkflowView,
  event: Extract<ExecutionStreamEvent, { type: "ExecutionStatusChanged" }>
): WorkflowView {
  const nextStatus = normalizeToWorkflowStatus(event.to);
  return { ...execution, status: nextStatus };
}

function applyNodeCancelled(execution: WorkflowView, event: Extract<ExecutionStreamEvent, { type: "NodeCancelled" }>): WorkflowView {
  const reason = event.cancel?.reason ?? undefined;
  return {
    ...execution,
    nodes: upsertNode(execution.nodes, {
      nodeId: event.nodeId,
      status: "CANCELED",
      canceledByExecution: true,
      cancelReason: reason
    })
  };
}

function applyNodeFailed(execution: WorkflowView, event: Extract<ExecutionStreamEvent, { type: "NodeFailed" }>): WorkflowView {
  const error =
    event.error == null ? undefined : { message: event.error.message ?? undefined };
  return {
    ...execution,
    nodes: upsertNode(execution.nodes, {
      nodeId: event.nodeId,
      status: "FAILED",
      error
    })
  };
}

export function parseExecutionStreamEvent(payload: string): ExecutionStreamEvent | null {
  if (!payload) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(payload);
  } catch {
    return null;
  }

  if (!parsed || typeof parsed !== "object") return null;
  const event = parsed as Record<string, unknown>;
  if (typeof event.type !== "string" || typeof event.executionId !== "string") return null;

  const type = event.type;
  if (type !== "GraphUpdated" && type !== "ExecutionStatusChanged" && type !== "NodeCancelled" && type !== "NodeFailed") {
    return null;
  }

  return event as ExecutionStreamEvent;
}

/** v2: event.executionId は displayId として扱う。 */
export function applyExecutionStreamEvent(current: WorkflowView, event: ExecutionStreamEvent): WorkflowView {
  if (event.executionId !== current.displayId) return current;

  if (event.type === "GraphUpdated") return applyGraphUpdated(current, event);
  if (event.type === "ExecutionStatusChanged") return applyStatusChanged(current, event);
  if (event.type === "NodeCancelled") return applyNodeCancelled(current, event);
  return applyNodeFailed(current, event);
}
