import type {
  ExecutionDTO,
  ExecutionNodeDTO,
  ExecutionStatus,
  ExecutionStreamEvent,
  NodeStatus
} from "./types";

function normalizeExecutionStatus(value: string): ExecutionStatus {
  const normalized = value.trim().toUpperCase();
  if (normalized === "CANCELLED") return "CANCELED";
  if (normalized === "CANCELED") return "CANCELED";
  if (normalized === "COMPLETED") return "COMPLETED";
  if (normalized === "FAILED") return "FAILED";
  return "ACTIVE";
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

function applyGraphUpdated(execution: ExecutionDTO, event: Extract<ExecutionStreamEvent, { type: "GraphUpdated" }>): ExecutionDTO {
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
  execution: ExecutionDTO,
  event: Extract<ExecutionStreamEvent, { type: "ExecutionStatusChanged" }>
): ExecutionDTO {
  const nextStatus = normalizeExecutionStatus(event.to);
  const at = event.at ?? null;

  return {
    ...execution,
    status: nextStatus,
    canceledAt: nextStatus === "CANCELED" ? at : execution.canceledAt,
    failedAt: nextStatus === "FAILED" ? at : execution.failedAt,
    completedAt: nextStatus === "COMPLETED" ? at : execution.completedAt
  };
}

function applyNodeCancelled(execution: ExecutionDTO, event: Extract<ExecutionStreamEvent, { type: "NodeCancelled" }>): ExecutionDTO {
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

function applyNodeFailed(execution: ExecutionDTO, event: Extract<ExecutionStreamEvent, { type: "NodeFailed" }>): ExecutionDTO {
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

export function applyExecutionStreamEvent(current: ExecutionDTO, event: ExecutionStreamEvent): ExecutionDTO {
  if (event.executionId !== current.executionId) return current;

  if (event.type === "GraphUpdated") return applyGraphUpdated(current, event);
  if (event.type === "ExecutionStatusChanged") return applyStatusChanged(current, event);
  if (event.type === "NodeCancelled") return applyNodeCancelled(current, event);
  return applyNodeFailed(current, event);
}
