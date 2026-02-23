import type { PersistedEvent } from "../../infrastructure/persistence/repositories/event-store.js";

type GraphUpdatedEvent = {
  type: "GraphUpdated";
  executionId: string;
  patch: {
    nodes: Array<{
      nodeId: string;
      status?: string;
      attempt?: number;
      waitKey?: string | null;
      canceledByExecution?: boolean;
    }>;
  };
  at: string;
};

type ExecutionStatusChangedEvent = {
  type: "ExecutionStatusChanged";
  executionId: string;
  to: "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";
  at: string;
};

type NodeCancelledEvent = {
  type: "NodeCancelled";
  executionId: string;
  nodeId: string;
  cancel: {
    reason: string | null;
  };
  at: string;
};

type NodeFailedEvent = {
  type: "NodeFailed";
  executionId: string;
  nodeId: string;
  error: {
    message: string | null;
  };
  at: string;
};

export type StreamEvent = GraphUpdatedEvent | ExecutionStatusChangedEvent | NodeCancelledEvent | NodeFailedEvent;

function toObject(payload: unknown): Record<string, unknown> {
  return payload && typeof payload === "object" ? (payload as Record<string, unknown>) : {};
}

function pickNodeId(payload: Record<string, unknown>): string | null {
  return typeof payload.nodeId === "string" ? payload.nodeId : null;
}

export function mapPersistedEventToStreamEvent(event: PersistedEvent): StreamEvent | null {
  const payload = toObject(event.payload);

  if (event.type === "EXECUTION_STARTED") {
    return { type: "ExecutionStatusChanged", executionId: event.executionId, to: "ACTIVE", at: event.occurredAt };
  }
  if (event.type === "EXECUTION_COMPLETED") {
    return { type: "ExecutionStatusChanged", executionId: event.executionId, to: "COMPLETED", at: event.occurredAt };
  }
  if (event.type === "EXECUTION_FAILED") {
    return { type: "ExecutionStatusChanged", executionId: event.executionId, to: "FAILED", at: event.occurredAt };
  }
  if (event.type === "EXECUTION_CANCELED") {
    return { type: "ExecutionStatusChanged", executionId: event.executionId, to: "CANCELED", at: event.occurredAt };
  }

  if (event.type === "NODE_FAILED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    const error = toObject(payload.error);
    return {
      type: "NodeFailed",
      executionId: event.executionId,
      nodeId,
      error: {
        message: typeof error.message === "string" ? error.message : null
      },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_CANCELED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "NodeCancelled",
      executionId: event.executionId,
      nodeId,
      cancel: {
        reason: typeof payload.reason === "string" ? payload.reason : null
      },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_CREATED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: { nodes: [{ nodeId, status: "IDLE" }] },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_READY") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: { nodes: [{ nodeId, status: "READY" }] },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_STARTED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: {
        nodes: [
          {
            nodeId,
            status: "RUNNING",
            attempt: typeof payload.attempt === "number" ? payload.attempt : undefined
          }
        ]
      },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_WAITING") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: {
        nodes: [
          {
            nodeId,
            status: "WAITING",
            waitKey: typeof payload.waitKey === "string" ? payload.waitKey : null
          }
        ]
      },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_RESUMED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: { nodes: [{ nodeId, status: "RUNNING" }] },
      at: event.occurredAt
    };
  }

  if (event.type === "NODE_SUCCEEDED") {
    const nodeId = pickNodeId(payload);
    if (!nodeId) return null;
    return {
      type: "GraphUpdated",
      executionId: event.executionId,
      patch: { nodes: [{ nodeId, status: "SUCCEEDED" }] },
      at: event.occurredAt
    };
  }

  return null;
}
