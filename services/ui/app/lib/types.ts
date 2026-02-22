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
