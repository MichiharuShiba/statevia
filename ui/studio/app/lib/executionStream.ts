import type {
  ExecutionNodeDTO,
  ExecutionStreamEvent,
  NodeStatus,
  ExecutionStatus,
  ExecutionView
} from "./types";

function normalizeToExecutionStatus(value: string): ExecutionStatus {
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

function upsertNode(nodes: ExecutionNodeDTO[], incoming: Partial<ExecutionNodeDTO> & { executionNodeId: string }): ExecutionNodeDTO[] {
  const index = nodes.findIndex((node) => node.executionNodeId === incoming.executionNodeId);
  if (index < 0) {
    return [
      ...nodes,
      {
        executionNodeId: incoming.executionNodeId,
        stateName: incoming.stateName ?? "",
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
  const { executionNodeId: _ignoredExecutionNodeId, ...rest } = incoming;
  const definedPatch = Object.fromEntries(
    Object.entries(rest).filter(([, value]) => value !== undefined)
  ) as Partial<ExecutionNodeDTO>;
  const nextNode: ExecutionNodeDTO = { ...target, ...definedPatch, executionNodeId: target.executionNodeId };

  return [...nodes.slice(0, index), nextNode, ...nodes.slice(index + 1)];
}

function applyGraphUpdated(execution: ExecutionView, event: Extract<ExecutionStreamEvent, { type: "GraphUpdated" }>): ExecutionView {
  const patchNodes = event.patch.nodes ?? [];
  const nextNodes = patchNodes.reduce((acc, patchNode) => {
    const normalizedStatus = patchNode.status ? normalizeNodeStatus(patchNode.status) : null;
    return upsertNode(acc, {
      executionNodeId: patchNode.executionNodeId,
      stateName: patchNode.stateName,
      status: normalizedStatus ?? undefined,
      attempt: patchNode.attempt,
      workerId: patchNode.workerId,
      waitKey: patchNode.waitKey,
      canceledByExecution: patchNode.canceledByExecution,
      error: patchNode.error,
      cancelReason: patchNode.cancelReason
    });
  }, execution.nodes);

  return { ...execution, nodes: nextNodes };
}

function applyStatusChanged(
  execution: ExecutionView,
  event: Extract<ExecutionStreamEvent, { type: "ExecutionStatusChanged" }>
): ExecutionView {
  const nextStatus = normalizeToExecutionStatus(event.to);
  return { ...execution, status: nextStatus };
}

function applyNodeCancelled(execution: ExecutionView, event: Extract<ExecutionStreamEvent, { type: "NodeCancelled" }>): ExecutionView {
  const reason = event.cancel?.reason ?? undefined;
  return {
    ...execution,
    nodes: upsertNode(execution.nodes, {
      executionNodeId: event.nodeId,
      status: "CANCELED",
      canceledByExecution: true,
      cancelReason: reason
    })
  };
}

function applyNodeFailed(execution: ExecutionView, event: Extract<ExecutionStreamEvent, { type: "NodeFailed" }>): ExecutionView {
  const error =
    event.error == null ? undefined : { message: event.error.message ?? undefined };
  return {
    ...execution,
    nodes: upsertNode(execution.nodes, {
      executionNodeId: event.nodeId,
      status: "FAILED",
      error
    })
  };
}

/** SSE ペイロード文字列を実行イベントにパースする。 */
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
export function applyExecutionStreamEvent(current: ExecutionView, event: ExecutionStreamEvent): ExecutionView {
  if (event.executionId !== current.displayId) return current;

  if (event.type === "GraphUpdated") return applyGraphUpdated(current, event);
  if (event.type === "ExecutionStatusChanged") return applyStatusChanged(current, event);
  if (event.type === "NodeCancelled") return applyNodeCancelled(current, event);
  return applyNodeFailed(current, event);
}
