import type { ExecutionNodeDTO, NodeStatus } from "../types";

/**
 * 同名（循環再入）の実行ノードから、UI 表示・Resume に使う代表行を選ぶ優先度。
 * WAITING を最優先し、次に進行中、最後に attempt の大きい完了行へ寄せる。
 */
const STATUS_PRIORITY: Record<NodeStatus, number> = {
  WAITING: 400,
  RUNNING: 300,
  READY: 250,
  FAILED: 200,
  CANCELED: 200,
  SUCCEEDED: 100,
  IDLE: 0
};

/**
 * 同一 `nodeName` を持つ実行ノードから、グラフマージ／詳細パネル用の代表を選ぶ。
 *
 * @param nodes 実行ビューの全ノード。
 * @param nodeNameKey 定義上の状態名、または選択キー（大小無視・前後空白無視）。
 * @returns 該当が無ければ `undefined`。複数あれば WAITING → RUNNING → 高 attempt → 後ろ勝ち。
 */
export function pickPreferredRuntimeNode(
  nodes: readonly ExecutionNodeDTO[],
  nodeNameKey: string
): ExecutionNodeDTO | undefined {
  const key = nodeNameKey.trim().toLowerCase();
  if (key.length === 0) return undefined;

  const matches = nodes.filter((node) => {
    const name = typeof node.nodeName === "string" ? node.nodeName.trim().toLowerCase() : "";
    return name.length > 0 && name === key;
  });
  if (matches.length === 0) return undefined;

  const [first, ...rest] = matches;
  return rest.reduce((best, current) => {
    const bestPriority = STATUS_PRIORITY[best.status] ?? 0;
    const currentPriority = STATUS_PRIORITY[current.status] ?? 0;
    if (currentPriority !== bestPriority) {
      return currentPriority > bestPriority ? current : best;
    }
    if (current.attempt !== best.attempt) {
      return current.attempt > best.attempt ? current : best;
    }
    // 同等なら配列後方（通常は後から追加された訪問）を採用する。
    return current;
  }, first);
}
