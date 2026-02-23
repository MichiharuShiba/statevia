import type { PersistedEvent } from "../../infrastructure/persistence/repositories/event-store.js";

type GraphUpdatedNode = {
  nodeId: string;
  status?: string;
  attempt?: number;
  waitKey?: string | null;
  canceledByExecution?: boolean;
};

type GraphUpdatedEvent = {
  type: "GraphUpdated";
  executionId: string;
  patch: { nodes: GraphUpdatedNode[] };
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

function graphUpdated(
  executionId: string,
  at: string,
  nodes: GraphUpdatedNode[]
): GraphUpdatedEvent {
  return { type: "GraphUpdated", executionId, patch: { nodes }, at };
}

type MapHandler = (event: PersistedEvent, payload: Record<string, unknown>) => StreamEvent | null;

function executionStatus(event: PersistedEvent, to: ExecutionStatusChangedEvent["to"]): ExecutionStatusChangedEvent {
  return { type: "ExecutionStatusChanged", executionId: event.executionId, to, at: event.occurredAt };
}

function nodeFailed(event: PersistedEvent, payload: Record<string, unknown>): NodeFailedEvent | null {
  const nodeId = pickNodeId(payload);
  if (!nodeId) return null;
  const error = toObject(payload.error);
  return {
    type: "NodeFailed",
    executionId: event.executionId,
    nodeId,
    error: { message: typeof error.message === "string" ? error.message : null },
    at: event.occurredAt
  };
}

function nodeCancelled(event: PersistedEvent, payload: Record<string, unknown>): NodeCancelledEvent | null {
  const nodeId = pickNodeId(payload);
  if (!nodeId) return null;
  return {
    type: "NodeCancelled",
    executionId: event.executionId,
    nodeId,
    cancel: { reason: typeof payload.reason === "string" ? payload.reason : null },
    at: event.occurredAt
  };
}

function withNodeId(
  event: PersistedEvent,
  payload: Record<string, unknown>,
  build: (nodeId: string) => GraphUpdatedNode[]
): StreamEvent | null {
  const nodeId = pickNodeId(payload);
  if (!nodeId) return null;
  return graphUpdated(event.executionId, event.occurredAt, build(nodeId));
}

const EVENT_HANDLERS: Record<string, MapHandler> = {
  EXECUTION_STARTED: (e) => executionStatus(e, "ACTIVE"),
  EXECUTION_COMPLETED: (e) => executionStatus(e, "COMPLETED"),
  EXECUTION_FAILED: (e) => executionStatus(e, "FAILED"),
  EXECUTION_CANCELED: (e) => executionStatus(e, "CANCELED"),
  NODE_FAILED: nodeFailed,
  NODE_CANCELED: nodeCancelled,
  NODE_CREATED: (e, p) => withNodeId(e, p, (nodeId) => [{ nodeId, status: "IDLE" }]),
  NODE_READY: (e, p) => withNodeId(e, p, (nodeId) => [{ nodeId, status: "READY" }]),
  NODE_STARTED: (e, p) =>
    withNodeId(e, p, (nodeId) => [
      { nodeId, status: "RUNNING", attempt: typeof p.attempt === "number" ? p.attempt : undefined }
    ]),
  NODE_WAITING: (e, p) =>
    withNodeId(e, p, (nodeId) => [
      { nodeId, status: "WAITING", waitKey: typeof p.waitKey === "string" ? p.waitKey : null }
    ]),
  NODE_RESUMED: (e, p) => withNodeId(e, p, (nodeId) => [{ nodeId, status: "RUNNING" }]),
  NODE_SUCCEEDED: (e, p) => withNodeId(e, p, (nodeId) => [{ nodeId, status: "SUCCEEDED" }])
};

export function mapPersistedEventToStreamEvent(event: PersistedEvent): StreamEvent | null {
  const payload = toObject(event.payload);
  const handler = EVENT_HANDLERS[event.type];
  return handler ? handler(event, payload) : null;
}
