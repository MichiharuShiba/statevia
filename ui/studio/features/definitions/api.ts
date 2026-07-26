import {
  apiDelete,
  apiGet,
  apiPost,
  type PaginationQuery,
  type SortQuery,
} from "@/shared/api";
import type { DefinitionDTO, PagedDefinitions } from "./types";

export type { PaginationQuery, SortOrder, SortQuery } from "@/shared/api";

/** 定義一覧 API / 画面 URL のクエリパラメータ。 */
export type DefinitionsListQuery = {
  pagination: PaginationQuery;
  sort: SortQuery;
  name?: string;
  /** `true` のとき削除済み定義を含む（クエリ `includeDeleted=true`）。 */
  includeDeleted?: boolean;
};

/**
 * 定義一覧用の相対パス `...?...` を組み立てる。空のフィルタは含めない。
 * API（`/api/core/...`）と App Router の `/definitions` 双方で同じ形を使う。
 * @param params 一覧クエリ
 * @returns `/definitions?...`
 */
export function buildDefinitionsListPath(params: DefinitionsListQuery): string {
  const query = new URLSearchParams();
  query.set("limit", String(params.pagination.limit));
  query.set("offset", String(params.pagination.offset));
  if (params.name?.trim()) query.set("name", params.name.trim());
  if (params.sort.sortBy?.trim()) query.set("sortBy", params.sort.sortBy.trim());
  if (params.sort.sortOrder) query.set("sortOrder", params.sort.sortOrder);
  if (params.includeDeleted === true) query.set("includeDeleted", "true");
  return `/definitions?${query.toString()}`;
}

/**
 * 定義一覧を取得する。
 * @param query 一覧クエリ
 * @returns ページング結果
 */
export async function listDefinitions(query: DefinitionsListQuery): Promise<PagedDefinitions> {
  return apiGet<PagedDefinitions>(buildDefinitionsListPath(query));
}

/**
 * 定義 1 件を取得する。
 * @param displayId 定義 displayId
 */
export async function getDefinition(displayId: string): Promise<DefinitionDTO> {
  return apiGet<DefinitionDTO>(`/definitions/${encodeURIComponent(displayId)}`);
}

/**
 * 定義を catalog 論理削除する。
 * @param displayId 定義 displayId
 */
export async function softDeleteDefinition(displayId: string): Promise<void> {
  await apiDelete(`/definitions/${encodeURIComponent(displayId)}`);
}

/**
 * 論理削除済み定義を復元する。
 * @param displayId 定義 displayId
 * @returns 復元後の定義
 */
export async function restoreDefinition(displayId: string): Promise<DefinitionDTO> {
  return apiPost<DefinitionDTO>(`/definitions/${encodeURIComponent(displayId)}/restore`, {});
}

/** 定義起点の実行開始 API が返す最小結果（遷移に必要な displayId のみ）。 */
export type StartExecutionFromDefinitionResult = {
  displayId: string;
};

/**
 * 定義を起点に新規実行を開始する。
 * @param definitionId 定義 displayId
 * @param input 任意の開始 input（未指定時は body に含めない）
 * @returns 作成された実行の displayId
 */
export async function startExecutionFromDefinition(
  definitionId: string,
  input?: unknown,
): Promise<StartExecutionFromDefinitionResult> {
  const body: { definitionId: string; input?: unknown } = { definitionId };
  if (input !== undefined) {
    body.input = input;
  }
  return apiPost<StartExecutionFromDefinitionResult>("/executions", body);
}
