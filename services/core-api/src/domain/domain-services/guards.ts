/**
 * Guards ドメインサービス
 * ドメインの不変条件をチェックする
 */
import { ExecutionState } from "../value-objects/execution-state.js";
import { NodeStatus } from "../entities/node.js";
import { DomainError } from "../errors.js";

export function ensureNotTerminalExecution(s: ExecutionState): void {
  if (["COMPLETED", "FAILED", "CANCELED"].includes(s.status)) {
    throw new DomainError("EXECUTION_TERMINAL", "Execution is terminal", { status: s.status });
  }
}

export function ensureCancelNotRequested(s: ExecutionState): void {
  if (s.cancelRequestedAt) {
    throw new DomainError("EXECUTION_CANCEL_REQUESTED", "Execution is cancel-requested", { cancelRequestedAt: s.cancelRequestedAt });
  }
}

export function getNodeOrThrow(s: ExecutionState, nodeId: string) {
  const n = s.nodes[nodeId];
  if (!n) throw new DomainError("NODE_NOT_FOUND", "Node not found", { nodeId });
  return n;
}

export function ensureNodeStatus(nStatus: NodeStatus, allowed: NodeStatus[]): void {
  if (!allowed.includes(nStatus)) {
    throw new DomainError("NODE_STATUS_INVALID", "Node status invalid for this command", { nodeStatus: nStatus, allowed });
  }
}
