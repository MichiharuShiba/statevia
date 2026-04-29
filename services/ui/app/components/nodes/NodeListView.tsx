"use client";

import type { ExecutionNodeDTO } from "../../lib/types";
import { getNodeSortWeight, getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";

type NodeListViewProps = {
  nodes: ExecutionNodeDTO[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
};

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
            <th className="py-2 pr-2">{uiText.nodeList.columns.nodeId}</th>
            <th className="py-2 pr-2">{uiText.nodeList.columns.type}</th>
            <th className="py-2 pr-2">{uiText.nodeList.columns.status}</th>
            <th className="py-2 pr-2">{uiText.nodeList.columns.waitKey}</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((node) => {
            const style = getStatusStyle(node.status);
            const selected = selectedNodeId === node.nodeId;
            const runningClass = node.status === "RUNNING" ? "opacity-80" : "";
            return (
              <tr
                key={node.nodeId}
                className={`cursor-pointer border-t border-[var(--md-sys-color-outline)] ${style.bgClass} ${runningClass} ${selected ? "outline outline-2 outline-[var(--md-sys-color-primary)]" : ""}`}
                onClick={() => onSelectNode(node.nodeId)}
              >
                <td className="py-2 pr-2 font-mono text-xs">{node.nodeId}</td>
                <td className="py-2 pr-2">{node.nodeType}</td>
                <td className="py-2 pr-2">
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
                    {node.status}
                  </span>
                </td>
                <td className="py-2 pr-2 font-mono text-xs">{node.waitKey ?? "—"}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
