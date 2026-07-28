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
