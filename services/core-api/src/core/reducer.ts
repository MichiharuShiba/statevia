import { ExecutionState, ExecutionStatus, NodeStatus , EventEnvelope } from "./types.js";

const execRank: Record<ExecutionStatus, number> = {
  CANCELED: 400,
  FAILED: 300,
  COMPLETED: 200,
  ACTIVE: 100
};

const nodeRank: Record<NodeStatus, number> = {
  CANCELED: 700,
  FAILED: 600,
  SUCCEEDED: 500,
  WAITING: 400,
  RUNNING: 300,
  READY: 200,
  IDLE: 100
};

function chooseExecStatus(current: ExecutionStatus, candidate: ExecutionStatus): ExecutionStatus {
  return execRank[candidate] > execRank[current] ? candidate : current;
}

function chooseNodeStatus(current: NodeStatus, candidate: NodeStatus): NodeStatus {
  return nodeRank[candidate] > nodeRank[current] ? candidate : current;
}

function isCancelRequested(state: ExecutionState): boolean {
  return state.cancelRequestedAt != null;
}

function shouldIgnoreProgressEvent(state: ExecutionState, type: string): boolean {
  if (!isCancelRequested(state)) return false;
  return new Set([
    "NODE_READY",
    "NODE_STARTED",
    "NODE_PROGRESS_REPORTED",
    "NODE_WAITING",
    "NODE_RESUME_REQUESTED",
    "NODE_RESUMED",
    "JOIN_PASSED",
    "JOIN_GATE_UPDATED",
    "FORK_OPENED",
    "EXECUTION_COMPLETED",
    "EXECUTION_FAILED"
  ]).has(type);
}

function updateNodeStatus(state: ExecutionState, nodeId: string, candidate: NodeStatus): ExecutionState {
  const node = state.nodes[nodeId];
  if (!node) return state;
  const next = { ...node, status: chooseNodeStatus(node.status, candidate) };
  return { ...state, nodes: { ...state.nodes, [nodeId]: next } };
}

function normalize(state: ExecutionState): ExecutionState {
  if (state.status !== "CANCELED") return state;
  const nodes = { ...state.nodes };
  for (const [id, n] of Object.entries(nodes)) {
    if (["IDLE", "READY", "RUNNING", "WAITING"].includes(n.status)) {
      nodes[id] = {
        ...n,
        status: chooseNodeStatus(n.status, "CANCELED"),
        canceledByExecution: true
      };
    }
  }
  return { ...state, nodes };
}

export function reduce(state: ExecutionState, event: EventEnvelope): ExecutionState {
  if (event.schemaVersion !== 1) return state;

  if (shouldIgnoreProgressEvent(state, event.type)) return state;

  let s = state;

  switch (event.type) {
    case "EXECUTION_CREATED": {
      const graphId = (event.payload as any).graphId as string;
      s = { ...s, graphId, status: "ACTIVE" };
      break;
    }
    case "EXECUTION_STARTED":
      s = { ...s, status: chooseExecStatus(s.status, "ACTIVE") };
      break;

    case "EXECUTION_CANCEL_REQUESTED":
      s = { ...s, cancelRequestedAt: s.cancelRequestedAt ?? event.occurredAt };
      break;

    case "EXECUTION_CANCELED":
      s = {
        ...s,
        canceledAt: s.canceledAt ?? event.occurredAt,
        status: chooseExecStatus(s.status, "CANCELED")
      };
      break;

    case "EXECUTION_FAILED":
      s = {
        ...s,
        failedAt: s.failedAt ?? event.occurredAt,
        status: chooseExecStatus(s.status, "FAILED")
      };
      break;

    case "EXECUTION_COMPLETED":
      s = {
        ...s,
        completedAt: s.completedAt ?? event.occurredAt,
        status: chooseExecStatus(s.status, "COMPLETED")
      };
      break;

    case "NODE_CREATED": {
      const p = event.payload as any;
      if (s.nodes[p.nodeId]) break;
      s = {
        ...s,
        nodes: {
          ...s.nodes,
          [p.nodeId]: {
            nodeId: p.nodeId,
            nodeType: p.nodeType,
            status: "IDLE",
            attempt: 0
          }
        }
      };
      break;
    }

    case "NODE_READY":
      s = updateNodeStatus(s, (event.payload as any).nodeId, "READY");
      break;

    case "NODE_STARTED": {
      const p = event.payload as any;
      s = updateNodeStatus(s, p.nodeId, "RUNNING");
      const node = s.nodes[p.nodeId];
      if (node) {
        s = {
          ...s,
          nodes: {
            ...s.nodes,
            [p.nodeId]: {
              ...node,
              attempt: Math.max(node.attempt, p.attempt ?? 1),
              workerId: p.workerId ?? node.workerId
            }
          }
        };
      }
      break;
    }

    case "NODE_WAITING": {
      const p = event.payload as any;
      s = updateNodeStatus(s, p.nodeId, "WAITING");
      const node = s.nodes[p.nodeId];
      if (node) {
        s = { ...s, nodes: { ...s.nodes, [p.nodeId]: { ...node, waitKey: p.waitKey ?? node.waitKey } } };
      }
      break;
    }

    case "NODE_RESUMED":
      s = updateNodeStatus(s, (event.payload as any).nodeId, "RUNNING");
      break;

    case "NODE_SUCCEEDED": {
      const p = event.payload as any;
      s = updateNodeStatus(s, p.nodeId, "SUCCEEDED");
      const node = s.nodes[p.nodeId];
      if (node) s = { ...s, nodes: { ...s.nodes, [p.nodeId]: { ...node, output: p.output ?? node.output } } };
      break;
    }

    case "NODE_FAIL_REPORTED": {
      const p = event.payload as any;
      const node = s.nodes[p.nodeId];
      if (node) s = { ...s, nodes: { ...s.nodes, [p.nodeId]: { ...node, error: p.error ?? node.error } } };
      break;
    }

    case "NODE_FAILED": {
      const p = event.payload as any;
      s = updateNodeStatus(s, p.nodeId, "FAILED");
      const node = s.nodes[p.nodeId];
      if (node) s = { ...s, nodes: { ...s.nodes, [p.nodeId]: { ...node, error: p.error ?? node.error } } };
      break;
    }

    case "NODE_CANCELED":
      s = updateNodeStatus(s, (event.payload as any).nodeId, "CANCELED");
      break;

    default:
      // audit-only events: no-op
      break;
  }

  return normalize(s);
}