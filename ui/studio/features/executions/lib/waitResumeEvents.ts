/**
 * Wait ノードの Resume に使えるイベント名一覧を解決する。
 * `allowedEvents` を優先し、無ければ互換の `waitKey` を単一要素として返す。
 *
 * @param node Wait 関連フィールドを持つノード。
 * @returns Trim 済み・大文字小文字無視で重複除去したイベント名（空なら []）。
 */
export function resolveWaitResumeEvents(node: {
  allowedEvents?: readonly string[] | null;
  waitKey?: string | null;
}): string[] {
  const fromAllowed = (node.allowedEvents ?? [])
    .map((eventName) => eventName.trim())
    .filter((eventName) => eventName.length > 0);

  if (fromAllowed.length > 0) {
    return dedupeEventNames(fromAllowed);
  }

  const waitKey = typeof node.waitKey === "string" ? node.waitKey.trim() : "";
  return waitKey.length > 0 ? [waitKey] : [];
}

/**
 * 大文字小文字を無視してイベント名を一意化する（先勝ちで表記を保持）。
 *
 * @param eventNames Trim 済みイベント名。
 * @returns 重複除去後の一覧。
 */
function dedupeEventNames(eventNames: string[]): string[] {
  const seen = new Set<string>();
  const unique: string[] = [];
  for (const eventName of eventNames) {
    const key = eventName.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    unique.push(eventName);
  }
  return unique;
}

const TERMINAL_EXECUTION_STATUSES = new Set(["Completed", "Cancelled", "Failed"]);

/**
 * 上部「イベント送信」（PublishEvent シム）が使えるか。
 * Engine と同様に「WAITING の Wait がちょうど 1 件かつ許可イベントが 1 件」のときだけ true。
 *
 * @param execution 実行ビュー。未読込・終端・Cancel 要求済みなら false。
 * @returns PublishEvent UI を出してよいとき true。
 */
export function isPublishEventAvailable(execution: {
  status: string;
  cancelRequested?: boolean;
  nodes: ReadonlyArray<{
    status: string;
    allowedEvents?: readonly string[] | null;
    waitKey?: string | null;
  }>;
} | null): boolean {
  if (!execution) return false;
  if (execution.cancelRequested) return false;
  if (TERMINAL_EXECUTION_STATUSES.has(execution.status)) return false;

  const waitingNodes = execution.nodes.filter((node) => node.status === "WAITING");
  if (waitingNodes.length !== 1) return false;

  const [soleWaitingNode] = waitingNodes;
  if (!soleWaitingNode) return false;

  return resolveWaitResumeEvents(soleWaitingNode).length === 1;
}
