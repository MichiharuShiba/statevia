import type { ActionSchemaIndexItem, ActionSchemaIndexResponse } from "./types";

let cachedItems: ActionSchemaIndexItem[] | null = null;
let inflightRequest: Promise<ActionSchemaIndexItem[]> | null = null;

/**
 * セッション内の Action Schema index を返す（未取得なら fetcher で1回だけ取得）。
 */
export async function loadActionSchemaIndex(
  fetcher: () => Promise<ActionSchemaIndexResponse>
): Promise<ActionSchemaIndexItem[]> {
  if (cachedItems) {
    return cachedItems;
  }
  if (inflightRequest !== null) {
    return inflightRequest;
  }
  inflightRequest = fetcher()
    .then((response) => {
      cachedItems = response.items;
      return response.items;
    })
    .catch(() => {
      cachedItems = [];
      return [];
    })
    .finally(() => {
      inflightRequest = null;
    });
  return inflightRequest;
}
