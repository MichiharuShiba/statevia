/** Core-API エラー応答のクライアント側表現。 */
export type { ApiError } from "@/shared/api/apiError";

/** 実行ドメイン型の正本: `features/executions/types`。 */
export type {
  ExecutionStatus,
  NodeStatus,
  ExecutionDTO,
  ExecutionGraphNodeDTO,
  ExecutionGraphEdgeDTO,
  RuntimeGraphEdgeDTO,
  ExecutionGraphDTO,
  ExecutionNodeDTO,
  ExecutionView,
  CommandAccepted,
  GraphPatchNode,
  GraphUpdatedEvent,
  ExecutionStatusChangedEvent,
  NodeCancelledEvent,
  NodeFailedEvent,
  ExecutionStreamEvent,
  ExecutionEventWithSeq,
  ExecutionEventsResponse,
  PagedResult,
  PagedExecutions,
} from "@/features/executions/types";

/** GET /v1/definitions の要素（正本: `features/definitions/types`）。 */
export type { DefinitionDTO, PagedDefinitions } from "@/features/definitions/types";

/** 定義スキーマ取得 API のレスポンス（正本: `features/definition-editor/types`）。 */
export type { DefinitionSchemaResponse } from "@/features/definition-editor/types";
