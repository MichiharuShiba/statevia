/** v2: C# API のワークフロー状態。 */
export type WorkflowStatus = "Running" | "Completed" | "Cancelled" | "Failed";

export type NodeStatus = "IDLE" | "READY" | "RUNNING" | "WAITING" | "SUCCEEDED" | "FAILED" | "CANCELED";

/** v2: GET /v1/workflows/:id のレスポンス（C# WorkflowResponse）。 */
export type WorkflowDTO = {
  displayId: string;
  resourceId: string;
  status: WorkflowStatus;
  startedAt: string;
  updatedAt?: string | null;
  cancelRequested: boolean;
  restartLost: boolean;
};

/** v2: GET /v1/workflows/:id/graph のノード（C# ExecutionNode）。JSON は camelCase（Core-API）。 */
export type WorkflowGraphNodeDTO = {
  nodeId?: string;
  stateName?: string;
  startedAt?: string;
  completedAt?: string | null;
  fact?: string | null;
  output?: unknown;
};

/** v2: GET /v1/workflows/:id/graph の辺（C# ExecutionEdge）。 */
export type WorkflowGraphEdgeDTO = {
  fromNodeId?: string;
  toNodeId?: string;
  type?: number;
};

/** v2: GET /v1/workflows/:id/graph のレスポンス。 */
export type WorkflowGraphDTO = {
  nodes: WorkflowGraphNodeDTO[];
  edges: WorkflowGraphEdgeDTO[];
};

/** グラフ可視化用のノード（状態実行）。v2 では WorkflowGraphDTO から変換。 */
export type ExecutionNodeDTO = {
  nodeId: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  workerId: string | null;
  waitKey: string | null;
  canceledByExecution: boolean;
  error?: { message?: string } | null;
  cancelReason?: string | null;
};

/** v2: ワークフロー + グラフから組み立てたビュー。一覧・詳細・グラフで利用。 */
export type WorkflowView = WorkflowDTO & {
  graphId: string;
  nodes: ExecutionNodeDTO[];
};

export type CommandAccepted = {
  executionId: string; // v2 では displayId を格納
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

export type ExecutionEventWithSeq = { seq: number } & ExecutionStreamEvent;

export type ExecutionEventsResponse = {
  events: ExecutionEventWithSeq[];
  hasMore?: boolean;
};
