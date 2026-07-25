import {
  apiGet,
  apiPost,
  type PaginationQuery,
  type SortQuery,
} from "@/shared/api";
import type {
  CommandAccepted,
  ExecutionDTO,
  ExecutionEventsResponse,
  ExecutionGraphDTO,
  PagedExecutions,
} from "./types";

export type { PaginationQuery, SortOrder, SortQuery } from "@/shared/api";

/** 実行一覧 API / 画面 URL のクエリパラメータ。 */
export type ExecutionsListQuery = {
  pagination: PaginationQuery;
  sort: SortQuery;
  status?: string;
  name?: string;
  definitionId?: string;
};

/**
 * 実行一覧用の相対パス `...?...` を組み立てる。空のフィルタは含めない。
 * API（`/api/core/...`）と App Router の `/executions` 双方で同じ形を使う。
 * @param params 一覧クエリ
 * @returns `/executions?...`
 */
export function buildExecutionsListPath(params: ExecutionsListQuery): string {
  const query = new URLSearchParams();
  query.set("limit", String(params.pagination.limit));
  query.set("offset", String(params.pagination.offset));
  if (params.status?.trim()) query.set("status", params.status.trim());
  if (params.name?.trim()) query.set("name", params.name.trim());
  if (params.definitionId?.trim()) query.set("definitionId", params.definitionId.trim());
  if (params.sort.sortBy?.trim()) query.set("sortBy", params.sort.sortBy.trim());
  if (params.sort.sortOrder) query.set("sortOrder", params.sort.sortOrder);
  return `/executions?${query.toString()}`;
}

/**
 * 実行一覧を取得する。
 * @param query 一覧クエリ
 */
export async function listExecutions(query: ExecutionsListQuery): Promise<PagedExecutions> {
  return apiGet<PagedExecutions>(buildExecutionsListPath(query));
}

/**
 * 実行 1 件を取得する。
 * @param displayId 実行 displayId
 */
export async function getExecution(displayId: string): Promise<ExecutionDTO> {
  return apiGet<ExecutionDTO>(`/executions/${encodeURIComponent(displayId)}`);
}

/**
 * 実行グラフ（ノード・辺）を取得する。
 * @param displayId 実行 displayId
 */
export async function getExecutionGraph(displayId: string): Promise<ExecutionGraphDTO> {
  return apiGet<ExecutionGraphDTO>(`/executions/${encodeURIComponent(displayId)}/graph`);
}

/**
 * 実行のキャンセルを要求する。
 * @param displayId 実行 displayId
 */
export async function cancelExecution(displayId: string): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(`/executions/${encodeURIComponent(displayId)}/cancel`, {
    reason: "ui",
  });
}

/**
 * 実行へイベントを送信する。
 * @param displayId 実行 displayId
 * @param eventName イベント名
 */
export async function publishExecutionEvent(
  displayId: string,
  eventName: string
): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(`/executions/${encodeURIComponent(displayId)}/events`, {
    name: eventName,
  });
}

/**
 * Wait ノードの Resume を要求する。
 * @param displayId 実行 displayId
 * @param executionNodeId ノード ID
 * @param resumeKey Wait の resumeKey（あれば）
 */
export async function resumeExecutionNode(
  displayId: string,
  executionNodeId: string,
  resumeKey?: string
): Promise<CommandAccepted> {
  return apiPost<CommandAccepted>(
    `/executions/${encodeURIComponent(displayId)}/nodes/${encodeURIComponent(executionNodeId)}/resume`,
    { resumeKey }
  );
}

/**
 * 実行イベント履歴を取得する。
 * @param displayId 実行 displayId
 * @param query 追加クエリ（fromSeq 等）
 */
export async function listExecutionEvents(
  displayId: string,
  query?: string
): Promise<ExecutionEventsResponse> {
  const suffix = query ? `?${query}` : "";
  return apiGet<ExecutionEventsResponse>(
    `/executions/${encodeURIComponent(displayId)}/events${suffix}`
  );
}

/**
 * 定義グラフを取得する（実行可視化の定義レイヤ）。
 * @param graphId グラフ ID
 */
export async function getGraphDefinitionRaw(graphId: string): Promise<unknown> {
  return apiGet<unknown>(`/graphs/${encodeURIComponent(graphId)}`);
}
