import type { DefinitionGraphDocument, DefinitionGraphNode } from "./types";

/**
 * Wait ノードの `events` マップを設定し、旧形式フィールドをクリアする。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param events イベント名 → 遷移先ノード名
 * @returns 更新後ドキュメント（対象が Wait でない場合は変更なし）
 * @remarks `events` 設定時は検証契約に合わせ `event` / `next` / `edges` を除去する。
 */
export function setWaitEvents(
  document: DefinitionGraphDocument,
  nodeName: string,
  events: Record<string, string>
): DefinitionGraphDocument {
  return mapWaitNode(document, nodeName, () => ({
    events: { ...events },
    event: undefined,
    next: undefined,
    edges: undefined
  }));
}

/**
 * 旧形式 Wait の単一 `event` 文字列だけを更新する（`next` / `edges` は維持）。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param event イベント名
 * @returns 更新後ドキュメント
 */
export function setLegacyWaitEvent(
  document: DefinitionGraphDocument,
  nodeName: string,
  event: string
): DefinitionGraphDocument {
  return mapWaitNode(document, nodeName, (node) => ({
    ...node,
    event,
    events: undefined
  }));
}

/**
 * 旧形式（`event` + `next` / `edges`）を `events` マップへ明示変換する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @returns 変換後ドキュメント。変換不能な場合は元ドキュメント
 * @remarks 遷移先は `next` を優先し、無ければ先頭 edge の `to` を使う。
 */
export function convertLegacyWaitToEvents(
  document: DefinitionGraphDocument,
  nodeName: string
): DefinitionGraphDocument {
  return mapWaitNode(document, nodeName, (node) => {
    const eventName = node.event?.trim() || "resume";
    const target =
      node.next?.trim() ||
      node.edges?.map((edge) => edge.to?.trim()).find(Boolean) ||
      "";
    return {
      events: { [eventName]: target },
      event: undefined,
      next: undefined,
      edges: undefined
    };
  });
}

/**
 * Wait ノード上の `events` にキャンバス接続先を反映する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param targetNodeName 接続先ノード名
 * @returns 更新後ドキュメント
 * @remarks
 * 空ターゲットの行があればそれを埋め、なければ一意なイベント名を追加する。
 * `events` が未定義の Wait は空マップとして扱い、新規イベント行を追加する。
 */
export function connectWaitEventTarget(
  document: DefinitionGraphDocument,
  nodeName: string,
  targetNodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait") {
    return document;
  }

  const current: Record<string, string> =
    node.events === undefined ? {} : { ...node.events };
  const emptyKey = Object.entries(current).find(([, target]) => target.trim().length === 0)?.[0];
  if (emptyKey === undefined) {
    current[allocateEventKey(current)] = targetNodeName;
  } else {
    current[emptyKey] = targetNodeName;
  }
  return setWaitEvents(document, nodeName, current);
}

/**
 * `events` マップから指定イベント行を削除する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param eventName 削除するイベント名
 * @returns 更新後ドキュメント
 */
export function removeWaitEvent(
  document: DefinitionGraphDocument,
  nodeName: string,
  eventName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait" || !node.events) {
    return document;
  }
  const nextEvents = Object.fromEntries(
    Object.entries(node.events).filter(([key]) => key !== eventName)
  );
  return setWaitEvents(document, nodeName, nextEvents);
}

/**
 * `events` の遷移先だけを差し替える（イベント名は維持）。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param eventName 対象イベント名
 * @param targetNodeName 新しい遷移先
 * @returns 更新後ドキュメント
 */
export function setWaitEventTarget(
  document: DefinitionGraphDocument,
  nodeName: string,
  eventName: string,
  targetNodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait" || !node.events || !(eventName in node.events)) {
    return document;
  }
  return setWaitEvents(document, nodeName, {
    ...node.events,
    [eventName]: targetNodeName
  });
}

/**
 * `events` マップのイベント名キーを変更する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @param fromEventName 変更前イベント名
 * @param toEventName 変更後イベント名
 * @returns 更新後ドキュメント
 */
export function renameWaitEventKey(
  document: DefinitionGraphDocument,
  nodeName: string,
  fromEventName: string,
  toEventName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait" || !node.events || !(fromEventName in node.events)) {
    return document;
  }
  if (fromEventName === toEventName) {
    return document;
  }
  const nextEvents: Record<string, string> = {};
  for (const [key, target] of Object.entries(node.events)) {
    if (key === fromEventName) {
      nextEvents[toEventName] = target;
      continue;
    }
    nextEvents[key] = target;
  }
  return setWaitEvents(document, nodeName, nextEvents);
}

/**
 * 空のイベント行を 1 件追加する。
 *
 * @param document 定義グラフドキュメント
 * @param nodeName 対象 Wait ノード名
 * @returns 更新後ドキュメント
 * @remarks `events` が未定義の Wait は空マップとして扱い、新規行を追加する。
 */
export function addWaitEventRow(
  document: DefinitionGraphDocument,
  nodeName: string
): DefinitionGraphDocument {
  const node = document.nodes.find((entry) => entry.name === nodeName);
  if (node?.type !== "wait") {
    return document;
  }
  const current: Record<string, string> =
    node.events === undefined ? {} : { ...node.events };
  current[allocateEventKey(current)] = "";
  return setWaitEvents(document, nodeName, current);
}

function allocateEventKey(events: Record<string, string>): string {
  let suffix = 1;
  let key = `event${suffix}`;
  while (Object.hasOwn(events, key)) {
    suffix += 1;
    key = `event${suffix}`;
  }
  return key;
}

function mapWaitNode(
  document: DefinitionGraphDocument,
  nodeName: string,
  updater: (node: DefinitionGraphNode) => Partial<DefinitionGraphNode>
): DefinitionGraphDocument {
  let changed = false;
  const nodes = document.nodes.map((node) => {
    if (node.name !== nodeName || node.type !== "wait") {
      return node;
    }
    changed = true;
    return { ...node, ...updater(node), name: node.name, type: "wait" as const };
  });
  return changed ? { ...document, nodes } : document;
}
