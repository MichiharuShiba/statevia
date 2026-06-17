import type { ActionSchemaIndexItem } from "./types";

/**
 * actionId / displayName で Action Schema index 候補を絞り込む。
 * クエリ空のときは全件（スクロール一覧用）、入力時は上限件数まで返す。
 */
export function filterActionSchemaCandidates(
  candidates: ReadonlyArray<ActionSchemaIndexItem>,
  query: string,
  maxFilteredResults = 50
): ActionSchemaIndexItem[] {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) {
    return [...candidates];
  }
  return candidates
    .filter(
      (item) =>
        item.actionId.toLowerCase().includes(normalizedQuery) ||
        item.displayName.toLowerCase().includes(normalizedQuery)
    )
    .slice(0, maxFilteredResults);
}
