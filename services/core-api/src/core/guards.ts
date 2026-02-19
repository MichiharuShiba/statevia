import { ExecutionState, NodeStatus } from "./types.js";
import { conflict, notFound } from "../http/errors.js";

export function ensureNotTerminalExecution(s: ExecutionState) {
  if (["COMPLETED", "FAILED", "CANCELED"].includes(s.status)) {
    throw conflict("Execution is terminal", { status: s.status });
  }
}

export function ensureCancelNotRequested(s: ExecutionState) {
  if (s.cancelRequestedAt) {
    throw conflict("Execution is cancel-requested", { cancelRequestedAt: s.cancelRequestedAt });
  }
}

export function getNodeOrThrow(s: ExecutionState, nodeId: string) {
  const n = s.nodes[nodeId];
  if (!n) throw notFound("Node not found", { nodeId });
  return n;
}

export function ensureNodeStatus(nStatus: NodeStatus, allowed: NodeStatus[]) {
  if (!allowed.includes(nStatus)) {
    throw conflict("Node status invalid for this command", { nodeStatus: nStatus, allowed });
  }
}