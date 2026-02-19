export type ExecutionStatus = "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";
export type NodeStatus =
  | "IDLE"
  | "READY"
  | "RUNNING"
  | "WAITING"
  | "SUCCEEDED"
  | "FAILED"
  | "CANCELED";

export type Actor = {
  kind: "system" | "user" | "scheduler" | "external";
  id?: string;
};

export type EventEnvelope<TType extends string = string, TPayload = unknown> = {
  eventId: string; // uuid
  executionId: string;
  type: TType;
  occurredAt: string; // RFC3339
  actor: Actor;
  correlationId?: string;
  causationId?: string;
  schemaVersion: 1;
  payload: TPayload;
};

export type NodeState = {
  nodeId: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  workerId?: string;
  waitKey?: string;
  output?: unknown;
  error?: unknown;
  canceledByExecution?: boolean;
};

export type ExecutionState = {
  executionId: string;
  graphId: string;
  status: ExecutionStatus;
  cancelRequestedAt?: string;
  canceledAt?: string;
  failedAt?: string;
  completedAt?: string;
  version: number;
  nodes: Record<string, NodeState>;
};