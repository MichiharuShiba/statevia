export type ExecutionStatus = "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";
export type NodeStatus = "IDLE" | "READY" | "RUNNING" | "WAITING" | "SUCCEEDED" | "FAILED" | "CANCELED";

export type ExecutionNodeDTO = {
  nodeId: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  workerId: string | null;
  waitKey: string | null;
  canceledByExecution: boolean;
  /** 失敗時のみ。API/SSE から設定 */
  error?: { message?: string } | null;
  /** Cancel 時。SSE NodeCancelled から設定（API では未永続化） */
  cancelReason?: string | null;
};

export type ExecutionDTO = {
  executionId: string;
  status: ExecutionStatus;
  graphId: string;
  cancelRequestedAt: string | null;
  canceledAt: string | null;
  failedAt: string | null;
  completedAt: string | null;
  nodes: ExecutionNodeDTO[];
};

export type CommandAccepted = {
  executionId: string;
  command: string;
  accepted: true;
  correlationId?: string | null;
  idempotencyKey: string;
};

export type ApiError = {
  status?: number;
  error: {
    code: string;
    message: string;
    details?: Record<string, unknown>;
  };
};

export type GraphPatchNode = {
  nodeId: string;
  status?: NodeStatus;
  attempt?: number;
  waitKey?: string | null;
  canceledByExecution?: boolean;
  error?: { message?: string } | null;
  cancelReason?: string | null;
};

export type GraphUpdatedEvent = {
  type: "GraphUpdated";
  executionId: string;
  patch: {
    nodes?: GraphPatchNode[];
  };
  at?: string;
};

export type ExecutionStatusChangedEvent = {
  type: "ExecutionStatusChanged";
  executionId: string;
  from?: string;
  to: string;
  reason?: string;
  at?: string;
};

export type NodeCancelledEvent = {
  type: "NodeCancelled";
  executionId: string;
  nodeId: string;
  cancel?: {
    reason?: string;
    cause?: {
      message?: string;
      at?: string;
    };
  };
  at?: string;
};

export type NodeFailedEvent = {
  type: "NodeFailed";
  executionId: string;
  nodeId: string;
  error?: {
    message?: string;
    at?: string;
  };
  at?: string;
};

export type ExecutionStreamEvent =
  | GraphUpdatedEvent
  | ExecutionStatusChangedEvent
  | NodeCancelledEvent
  | NodeFailedEvent;
