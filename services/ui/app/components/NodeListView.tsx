"use client";

import type { ExecutionNodeDTO } from "../lib/types";
import { getNodeSortWeight, getStatusStyle } from "../lib/statusStyle";

type NodeListViewProps = {
  nodes: ExecutionNodeDTO[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string) => void;
};

export function NodeListView({ nodes, selectedNodeId, onSelectNode }: NodeListViewProps) {
  const sorted = [...nodes].sort((a, b) => getNodeSortWeight(a.status) - getNodeSortWeight(b.status));

  return (
    <div className="overflow-auto rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold">Nodes</h2>
        <span className="text-xs text-zinc-500">{nodes.length} nodes</span>
      </div>
      <table className="w-full text-left text-sm">
        <thead className="text-xs text-zinc-600">
          <tr>
            <th className="py-2 pr-2">nodeId</th>
            <th className="py-2 pr-2">type</th>
            <th className="py-2 pr-2">status</th>
            <th className="py-2 pr-2">waitKey</th>
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
                className={`cursor-pointer border-t border-zinc-100 ${style.bgClass} ${runningClass} ${selected ? "outline outline-2 outline-zinc-400" : ""}`}
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

