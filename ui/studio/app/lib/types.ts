/** v2: C# API の実行状態（ExecutionStatus）。 */
export type ExecutionStatus = "Running" | "Completed" | "Cancelled" | "Failed";

/** 実行ノードの状態（Engine / Core-API 準拠）。 */
export type NodeStatus = "IDLE" | "READY" | "RUNNING" | "WAITING" | "SUCCEEDED" | "FAILED" | "CANCELED";

/** v2: GET /v1/executions/:id のレスポンス（C# ExecutionResponse）。 */
export type ExecutionDTO = {
  displayId: string;
  resourceId: string;
  graphId: string;
  status: ExecutionStatus;
  startedAt: string;
  updatedAt?: string | null;
  cancelRequested: boolean;
  restartLost: boolean;
};

/**
 * v2: GET /v1/executions/:id/graph のノード（C# ExecutionNode）。JSON は camelCase（Core-API）。
 * ノード ID のキーは API／Engine／永続グラフとも `nodeId` のまま。UI 組み立て後の `ExecutionNodeDTO` だけ `executionNodeId` に正規化する。
 */
export type ExecutionGraphNodeDTO = {
  nodeId?: string;
  stateName?: string;
  nodeType?: string;
  startedAt?: string;
  completedAt?: string | null;
  fact?: string | null;
  input?: unknown;
  output?: unknown;
  attempt?: number;
  workerId?: string | null;
  waitKey?: string | null;
  canceledByExecution?: boolean;
  conditionRouting?: unknown;
};

/** v2: GET /v1/executions/:id/graph の辺（C# ExecutionEdge）。 */
export type ExecutionGraphEdgeDTO = {
  from?: string;
  to?: string;
  type?: number;
};

/** ランタイムグラフの辺（from / to / type）。 */
export type RuntimeGraphEdgeDTO = {
  from: string;
  to: string;
  type?: number;
};

/** v2: GET /v1/executions/:id/graph のレスポンス（C# ExecutionGraphResponse）。 */
export type ExecutionGraphDTO = {
  nodes: ExecutionGraphNodeDTO[];
  edges: ExecutionGraphEdgeDTO[];
};

/** グラフ可視化用のノード（状態実行）。v2 では ExecutionGraphDTO から変換。 */
export type ExecutionNodeDTO = {
  /** UI 向けの実行ノード ID。永続グラフ JSON のキーは `nodeId` のまま（API／Engine は変更しない）。グラフから組み立てるときはその値と同じ。 */
  executionNodeId: string;
  stateName?: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  workerId: string | null;
  waitKey: string | null;
  canceledByExecution: boolean;
  /** GET /graph のノードに含まれる場合のみ（ノード詳細のトレース用）。 */
  startedAt?: string;
  completedAt?: string | null;
  input?: unknown;
  output?: unknown;
  conditionRouting?: unknown;
  error?: { message?: string } | null;
  cancelReason?: string | null;
};

/** v2: 実行 + グラフから組み立てたビュー。一覧・詳細・グラフで利用。 */
export type ExecutionView = ExecutionDTO & {
  graphId: string;
  nodes: ExecutionNodeDTO[];
  runtimeEdges?: RuntimeGraphEdgeDTO[];
};

/** コマンド受理レスポンス（Core-API）。 */
export type CommandAccepted = {
  executionId: string; // v2 では displayId を格納
  command: string;
  accepted: true;
  correlationId?: string | null;
  idempotencyKey: string;
};

/** Core-API エラー応答のクライアント側表現。 */
export type ApiError = {
  status?: number;
  error: {
    code: string;
    message: string;
    details?: Record<string, unknown>;
  };
};

/** GraphUpdated 等のパッチ。`executionNodeId` は UI／REST 向け名（永続グラフ JSON の `nodeId` と同値）。 */
export type GraphPatchNode = {
  executionNodeId: string;
  stateName?: string;
  nodeType?: string;
  status?: NodeStatus;
  attempt?: number;
  workerId?: string | null;
  waitKey?: string | null;
  canceledByExecution?: boolean;
  error?: { message?: string } | null;
  cancelReason?: string | null;
};

/** SSE: グラフノードが更新されたイベント。 */
export type GraphUpdatedEvent = {
  type: "GraphUpdated";
  executionId: string;
  patch: {
    nodes?: GraphPatchNode[];
  };
  at?: string;
};

/** SSE: 実行状態が変化したイベント。 */
export type ExecutionStatusChangedEvent = {
  type: "ExecutionStatusChanged";
  executionId: string;
  from?: string;
  to: string;
  reason?: string;
  at?: string;
};

/** SSE: ノードがキャンセルされたイベント。 */
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

/** SSE: ノードが失敗したイベント。 */
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

/** 実行ストリームのイベント判別共用体。 */
export type ExecutionStreamEvent =
  | GraphUpdatedEvent
  | ExecutionStatusChangedEvent
  | NodeCancelledEvent
  | NodeFailedEvent;

/** シーケンス番号付き実行イベント。 */
export type ExecutionEventWithSeq = { seq: number } & ExecutionStreamEvent;

/** 実行イベント一覧 API のレスポンス。 */
export type ExecutionEventsResponse = {
  events: ExecutionEventWithSeq[];
  hasMore?: boolean;
};

/** GET /v1/executions?limit=&offset= のページング結果（Core-API `PagedResult<T>`）。 */
export type PagedResult<T> = {
  items: T[];
  totalCount: number;
  offset: number;
  limit: number;
  hasMore: boolean;
};

/** ページング付き実行一覧。 */
export type PagedExecutions = PagedResult<ExecutionDTO>;

/** GET /v1/definitions の要素（Core-API `DefinitionResponse`）。 */
export type DefinitionDTO = {
  displayId: string;
  resourceId: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  yaml?: string;
  /** 最新版番号（API が返す場合）。 */
  latestVersion?: number;
  /**
   * catalog 論理削除日時（UTC）。
   * `includeDeleted=true` の一覧でのみ truthy になりうる。通常 GET / restore 成功では含めない。
   */
  deletedAt?: string | null;
};

/** ページング付き定義一覧。 */
export type PagedDefinitions = PagedResult<DefinitionDTO>;

/** 定義スキーマ取得 API のレスポンス。 */
export type DefinitionSchemaResponse = {
  schemaVersion: string;
  nodesVersion: number;
  schema: Record<string, unknown>;
};
