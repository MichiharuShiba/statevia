import type { NodeStatus, WorkflowView } from "./types";

const FAILURE_OR_CANCEL: Set<NodeStatus> = new Set(["FAILED", "CANCELED"]);

function isFailureOrCancel(status: NodeStatus): boolean {
  return FAILURE_OR_CANCEL.has(status);
}

export type NodeDiffKind = "status_diff" | "only_in_left" | "only_in_right";

export type NodeDiffItem = {
  nodeId: string;
  kind: NodeDiffKind;
  statusLeft: NodeStatus | null;
  statusRight: NodeStatus | null;
  /** どちらかが FAILED/CANCELED で差がある、または片方のみでそのノードが FAILED/CANCELED */
  isFailureOrCancel: boolean;
};

export type ExecutionDiffResult = {
  /** ノード単位の差分一覧（Failed/Canceled を先頭にした並び） */
  nodeDiffs: NodeDiffItem[];
  /** グラフハイライト用: nodeId -> ハイライト情報 */
  nodeHighlights: Record<string, { isFailureOrCancel: boolean }>;
  /** 左（A）にのみ存在する nodeId */
  onlyInLeft: string[];
  /** 右（B）にのみ存在する nodeId */
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
  left: WorkflowView | null,
  right: WorkflowView | null
): ExecutionDiffResult | null {
  if (!left || !right) return null;

  const nodesLeft = new Map(left.nodes.map((n) => [n.nodeId, n]));
  const nodesRight = new Map(right.nodes.map((n) => [n.nodeId, n]));
  const allNodeIds = new Set<string>([...nodesLeft.keys(), ...nodesRight.keys()]);

  const nodeDiffs: NodeDiffItem[] = [];
  const nodeHighlights: Record<string, { isFailureOrCancel: boolean }> = {};
  const onlyInLeft: string[] = [];
  const onlyInRight: string[] = [];

  for (const nodeId of allNodeIds) {
    const nLeft = nodesLeft.get(nodeId) ?? null;
    const nRight = nodesRight.get(nodeId) ?? null;

    if (nLeft && !nRight) {
      const isFC = isFailureOrCancel(nLeft.status);
      nodeDiffs.push({
        nodeId,
        kind: "only_in_left",
        statusLeft: nLeft.status,
        statusRight: null,
        isFailureOrCancel: isFC
      });
      nodeHighlights[nodeId] = { isFailureOrCancel: isFC };
      onlyInLeft.push(nodeId);
      continue;
    }
    if (!nLeft && nRight) {
      const isFC = isFailureOrCancel(nRight.status);
      nodeDiffs.push({
        nodeId,
        kind: "only_in_right",
        statusLeft: null,
        statusRight: nRight.status,
        isFailureOrCancel: isFC
      });
      nodeHighlights[nodeId] = { isFailureOrCancel: isFC };
      onlyInRight.push(nodeId);
      continue;
    }
    if (nLeft && nRight && nLeft.status !== nRight.status) {
      const isFC =
        isFailureOrCancel(nLeft.status) || isFailureOrCancel(nRight.status);
      nodeDiffs.push({
        nodeId,
        kind: "status_diff",
        statusLeft: nLeft.status,
        statusRight: nRight.status,
        isFailureOrCancel: isFC
      });
      nodeHighlights[nodeId] = { isFailureOrCancel: isFC };
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
