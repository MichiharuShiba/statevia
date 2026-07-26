import type { ActionSchemaIndexItem } from "./types";

/**
 * actionId が Schema index（combobox 候補）に含まれるかを判定する。
 */
export function isIndexedActionId(
  actionId: string,
  candidates: ReadonlyArray<ActionSchemaIndexItem>
): boolean {
  const trimmed = actionId.trim();
  if (!trimmed) {
    return false;
  }
  return candidates.some((candidate) => candidate.actionId === trimmed);
}

/**
 * Schema index 候補の actionId 集合を構築する。
 */
export function buildIndexedActionIdSet(
  candidates: ReadonlyArray<ActionSchemaIndexItem>
): ReadonlySet<string> {
  return new Set(candidates.map((candidate) => candidate.actionId));
}
