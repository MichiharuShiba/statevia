"use client";

import type { ExecutionNodeDTO } from "../../lib/types";
import { formatExecutionDuration } from "../../lib/dateTime";
import { getNodeSortWeight, getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";

type NodeListViewProps = {
  nodes: ExecutionNodeDTO[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
};

/** 一覧の「ノード名」列: `stateName` があればそのまま、なければダッシュ。 */
function listNodeName(node: ExecutionNodeDTO): string {
  const trimmed = typeof node.stateName === "string" ? node.stateName.trim() : "";
  return trimmed.length > 0 ? trimmed : "—";
}

/** 一覧の「実行時間」列: 開始・終了から算出。算出不可はダッシュ。 */
function listDurationText(node: ExecutionNodeDTO): string {
  return formatExecutionDuration(node.startedAt, node.completedAt) ?? "—";
}

/**
 * 実行ノードの一覧（ステータス・タイプ・ノード名・実行ノード ID・実行時間）。
 */
export function NodeListView({ nodes, selectedNodeId, onSelectNode }: Readonly<NodeListViewProps>) {
  const uiText = useUiText();
  const sorted = [...nodes].sort((a, b) => getNodeSortWeight(a.status) - getNodeSortWeight(b.status));

  return (
    <div className="overflow-auto rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold">{uiText.nodeList.title}</h2>
        <span className="text-xs text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeList.nodeCount(nodes.length)}</span>
      </div>
      <table className="w-full text-left text-sm">
        <thead className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
          <tr>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.status}</th>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.type}</th>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.nodeName}</th>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.executionNodeId}</th>
            <th className="py-2 pl-2 pr-2">{uiText.nodeList.columns.duration}</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((node) => {
            const style = getStatusStyle(node.status);
            const selected = selectedNodeId === node.executionNodeId;
            const runningClass = node.status === "RUNNING" ? "opacity-80" : "";
            return (
              <tr
                key={node.executionNodeId}
                className={`cursor-pointer border-t border-[var(--md-sys-color-outline)] ${style.bgClass} ${runningClass} ${selected ? "outline outline-2 outline-[var(--md-sys-color-primary)]" : ""}`}
                onClick={() => onSelectNode(node.executionNodeId)}
              >
                <td className="py-2 pl-2 pr-2">
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
                    {node.status}
                  </span>
                </td>
                <td className="py-2 pl-2 pr-2">{node.nodeType}</td>
                <td className="max-w-[10rem] truncate py-2 pl-2 pr-2 font-mono text-xs" title={listNodeName(node)}>
                  {listNodeName(node)}
                </td>
                <td className="py-2 pl-2 pr-2 font-mono text-xs">{node.executionNodeId}</td>
                <td className="py-2 pl-2 pr-2 font-mono text-xs">{listDurationText(node)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
