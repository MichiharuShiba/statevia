import type { NodeStatus, ExecutionView } from "./types";

const FAILURE_OR_CANCEL: Set<NodeStatus> = new Set(["FAILED", "CANCELED"]);

function isFailureOrCancel(status: NodeStatus): boolean {
  return FAILURE_OR_CANCEL.has(status);
}

/** 実行比較におけるノード差分の種別。 */
export type NodeDiffKind = "status_diff" | "only_in_left" | "only_in_right";

/** 実行比較の1ノード分の差分。 */
export type NodeDiffItem = {
  executionNodeId: string;
  kind: NodeDiffKind;
  statusLeft: NodeStatus | null;
  statusRight: NodeStatus | null;
  /** どちらかが FAILED/CANCELED で差がある、または片方のみでそのノードが FAILED/CANCELED */
  isFailureOrCancel: boolean;
};

/** 2つの実行ビューの差分結果。 */
export type ExecutionDiffResult = {
  /** ノード単位の差分一覧（Failed/Canceled を先頭にした並び） */
  nodeDiffs: NodeDiffItem[];
  /** グラフハイライト用: executionNodeId -> ハイライト情報 */
  nodeHighlights: Record<string, { isFailureOrCancel: boolean }>;
  /** 左（A）にのみ存在する executionNodeId */
  onlyInLeft: string[];
  /** 右（B）にのみ存在する executionNodeId */
  onlyInRight: string[];
};

function nodeDiffSortKey(item: NodeDiffItem): number {
  if (item.isFailureOrCancel) return 0;
  if (item.kind === "only_in_left" || item.kind === "only_in_right") return 1;
  return 2;
}

/**
 * 2つの Execution を比較し、ノード単位の差分を算出する。
 * Failed/Canceled の差分を優先して並べる。
 */
export function computeExecutionDiff(
  left: ExecutionView | null,
  right: ExecutionView | null
): ExecutionDiffResult | null {
  if (!left || !right) return null;

  const nodesLeft = new Map(left.nodes.map((n) => [n.executionNodeId, n]));
  const nodesRight = new Map(right.nodes.map((n) => [n.executionNodeId, n]));
  const allNodeIds = new Set<string>([...nodesLeft.keys(), ...nodesRight.keys()]);

  const nodeDiffs: NodeDiffItem[] = [];
  const nodeHighlights: Record<string, { isFailureOrCancel: boolean }> = {};
  const onlyInLeft: string[] = [];
  const onlyInRight: string[] = [];

  for (const executionNodeId of allNodeIds) {
    const nLeft = nodesLeft.get(executionNodeId) ?? null;
    const nRight = nodesRight.get(executionNodeId) ?? null;

    if (nLeft && !nRight) {
      const isFC = isFailureOrCancel(nLeft.status);
      nodeDiffs.push({
        executionNodeId,
        kind: "only_in_left",
        statusLeft: nLeft.status,
        statusRight: null,
        isFailureOrCancel: isFC
      });
      nodeHighlights[executionNodeId] = { isFailureOrCancel: isFC };
      onlyInLeft.push(executionNodeId);
      continue;
    }
    if (!nLeft && nRight) {
      const isFC = isFailureOrCancel(nRight.status);
      nodeDiffs.push({
        executionNodeId,
        kind: "only_in_right",
        statusLeft: null,
        statusRight: nRight.status,
        isFailureOrCancel: isFC
      });
      nodeHighlights[executionNodeId] = { isFailureOrCancel: isFC };
      onlyInRight.push(executionNodeId);
      continue;
    }
    if (nLeft && nRight && nLeft.status !== nRight.status) {
      const isFC =
        isFailureOrCancel(nLeft.status) || isFailureOrCancel(nRight.status);
      nodeDiffs.push({
        executionNodeId,
        kind: "status_diff",
        statusLeft: nLeft.status,
        statusRight: nRight.status,
        isFailureOrCancel: isFC
      });
      nodeHighlights[executionNodeId] = { isFailureOrCancel: isFC };
    }
  }

  nodeDiffs.sort((a, b) => nodeDiffSortKey(a) - nodeDiffSortKey(b));

  return {
    nodeDiffs,
    nodeHighlights,
    onlyInLeft,
    onlyInRight
  };
}
