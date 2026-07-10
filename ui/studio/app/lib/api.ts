import { clearSessionAndRedirectToLogin } from "./authRedirect";
import type { ApiError } from "./types";

/** クライアント側の API 用設定（認証・テナント）。NEXT_PUBLIC_* または runtime で注入。 */
export function getApiConfig(): { tenantId: string; authToken: string } {
  if (typeof process !== "undefined" && process.env) {
    return {
      tenantId: process.env.NEXT_PUBLIC_TENANT_ID ?? "",
      authToken: process.env.NEXT_PUBLIC_AUTH_TOKEN ?? ""
    };
  }
  return { tenantId: "", authToken: "" };
}

/** REST リクエストに付与する認証・テナントヘッダを返す。 */
export function getApiHeaders(): Record<string, string> {
  const { tenantId, authToken } = getApiConfig();
  const headers: Record<string, string> = {};
  if (authToken) headers["Authorization"] = `Bearer ${authToken}`;
  if (tenantId) headers["X-Tenant-Id"] = tenantId;
  return headers;
}

function idem(): string {
  return crypto.randomUUID();
}

async function parseJsonSafe(res: Response): Promise<unknown> {
  const t = await res.text();
  try {
    return t ? JSON.parse(t) : null;
  } catch {
    // Return raw text when response is not valid JSON (e.g. plain error message)
    return t;
  }
}

function getErrorMessage(res: Response, json: unknown): string {
  if (typeof json === "object" && json !== null && "error" in json && typeof (json as ApiError).error?.message === "string") {
    return (json as ApiError).error.message;
  }
  if (typeof json === "string") return json;
  return res.statusText;
}

function getErrorCode(res: Response, json: unknown): string {
  if (typeof json === "object" && json !== null && "error" in json && typeof (json as ApiError).error?.code === "string") {
    return (json as ApiError).error.code;
  }
  return `HTTP_${res.status}`;
}

/** HTTP エラー応答を Error として投げる（`toToastError` の `ApiError` 形状を維持）。 */
function buildApiError(res: Response, json: unknown): Error & ApiError {
  const message = getErrorMessage(res, json);
  const apiError: ApiError = {
    status: res.status,
    error: { code: getErrorCode(res, json), message }
  };
  return Object.assign(new Error(message), apiError);
}

function toRecord(h: Headers | Record<string, string> | undefined): Record<string, string> {
  if (!h) return {};
  if (h instanceof Headers) return Object.fromEntries(h.entries());
  return { ...h };
}

async function fetchAndParse<T>(path: string, init?: RequestInit): Promise<T> {
  const mergedHeaders: Record<string, string> = {
    ...getApiHeaders(),
    ...toRecord(init?.headers as Headers | Record<string, string> | undefined)
  };
  const res = await fetch(`/api/core${path}`, {
    cache: "no-store",
    ...init,
    headers: mergedHeaders
  });
  const json = await parseJsonSafe(res);
  if (res.status === 401 && globalThis.window !== undefined) {
    void clearSessionAndRedirectToLogin();
    throw buildApiError(res, json);
  }
  if (!res.ok) {
    throw buildApiError(res, json);
  }
  return json as T;
}

/** `GET /v1/executions?limit&offset&status&name&definitionId` 向け（Core-API）。 */
export type PaginationQuery = {
  limit: number;
  offset: number;
};

/** ソート方向の共通型。 */
export type SortOrder = "asc" | "desc";

/** 一覧 API のソート指定。 */
export type SortQuery = {
  sortBy?: string;
  sortOrder?: SortOrder;
};

/** 定義一覧 API のクエリパラメータ。 */
export type DefinitionsListQuery = {
  pagination: PaginationQuery;
  sort: SortQuery;
  name?: string;
  /** `true` のとき削除済み定義を含む（クエリ `includeDeleted=true`）。 */
  includeDeleted?: boolean;
};

/** 実行一覧 API のクエリパラメータ。 */
export type ExecutionsListQuery = {
  pagination: PaginationQuery;
  sort: SortQuery;
  status?: string;
  name?: string;
  definitionId?: string;
};

/**
 * 実行一覧用の相対 API パス `...?...` を組み立てる。空のフィルタは含めない。
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
 * 定義一覧用の相対 API パス `...?...` を組み立てる。空のフィルタは含めない。
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

/** Core-API へ GET し JSON をパースして返す。 */
export async function apiGet<T>(path: string): Promise<T> {
  return fetchAndParse<T>(path);
}

/** Core-API へ POST（冪等キー付き）し JSON を返す。 */
export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  return fetchAndParse<T>(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Idempotency-Key": idem()
    },
    body: JSON.stringify(body ?? {})
  });
}

/** Core-API へ PUT（冪等キー付き）し JSON を返す。 */
export async function apiPut<T>(path: string, body: unknown): Promise<T> {
  return fetchAndParse<T>(path, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      "X-Idempotency-Key": idem()
    },
    body: JSON.stringify(body ?? {})
  });
}

/** Core-API へ PATCH（冪等キー付き）し JSON を返す。 */
export async function apiPatch<T>(path: string, body: unknown): Promise<T> {
  return fetchAndParse<T>(path, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      "X-Idempotency-Key": idem()
    },
    body: JSON.stringify(body ?? {})
  });
}

/** Core-API へ DELETE する（204 のときは undefined）。 */
export async function apiDelete(path: string): Promise<void> {
  await fetchAndParse<void>(path, { method: "DELETE" });
}
