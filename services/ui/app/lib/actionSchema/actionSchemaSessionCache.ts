import type { ActionSchemaDetailResponse } from "./types";

const actionSchemaDetailById = new Map<string, ActionSchemaDetailResponse>();

/**
 * セッション内の Action Schema 詳細キャッシュを返す。
 */
export function getCachedActionSchemaDetail(actionId: string): ActionSchemaDetailResponse | undefined {
  const trimmed = actionId.trim();
  return trimmed.length > 0 ? actionSchemaDetailById.get(trimmed) : undefined;
}

/**
 * セッション内の Action Schema 詳細キャッシュへ格納する。
 */
export function setCachedActionSchemaDetail(actionId: string, detail: ActionSchemaDetailResponse): void {
  const trimmed = actionId.trim();
  if (trimmed.length === 0) {
    return;
  }
  actionSchemaDetailById.set(trimmed, detail);
}
