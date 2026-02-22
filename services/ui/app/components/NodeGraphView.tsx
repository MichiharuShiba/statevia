"use client";

import { useMemo } from "react";
import ReactFlow, {
  Background,
  Controls,
  Handle,
  MarkerType,
  MiniMap,
  Position,
  type Edge,
  type Node,
  type NodeProps,
  type NodeTypes
} from "reactflow";
import "reactflow/dist/style.css";
import type { GroupBounds } from "../lib/grouping";
import type { PositionedEdge, PositionedNode } from "../lib/graphLayout";
import type { MergedGraphNode } from "../lib/mergeGraph";
import { getNodeAppearance } from "../lib/nodeAppearance";
import { getStatusStyle } from "../lib/statusStyle";
import type { NodeStatus } from "../lib/types";

type ExecutionNodeData = {
  nodeId: string;
  label: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  waitKey: string | null;
  selected: boolean;
  onSelect: (nodeId: string) => void;
  onResume: (nodeId: string) => void;
  resumeDisabledReason: string | null;
};

type GroupNodeData = {
  label: string;
};

function ExecutionNodeComponent({ data }: NodeProps<ExecutionNodeData>) {
  const style = getStatusStyle(data.status);
  const appearance = getNodeAppearance(data.nodeType);
  const isRunning = data.status === "RUNNING";
  const wrapClass = `${appearance.shapeClass} relative border-2 p-3 shadow-sm ${style.borderClass} ${style.bgClass} ${isRunning ? "opacity-80 text-zinc-600" : ""} ${data.selected ? "outline outline-2 outline-zinc-400" : ""}`;

  const content = (
    <div className={wrapClass} onClick={() => data.onSelect(data.nodeId)}>
      <Handle type="target" position={Position.Left} className="h-2 w-2 border-zinc-300 bg-zinc-200" />
      <div className="flex items-center justify-between gap-2 text-xs">
        <span>{appearance.icon}</span>
        <span className="font-semibold">{appearance.label}</span>
        <span className={`inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold ${style.badgeClass}`}>{data.status}</span>
      </div>
      <div className="mt-2 space-y-1 text-xs">
        <div className="font-mono">{data.label}</div>
        <div className="text-zinc-600">type: {data.nodeType}</div>
        <div className="text-zinc-600">attempt: {data.attempt}</div>
        {data.waitKey && <div className="text-zinc-600">waitKey: {data.waitKey}</div>}
      </div>
      {data.status === "WAITING" && (
        <div className="mt-3">
          <button
            className="w-full rounded-lg bg-amber-500 px-2 py-1.5 text-xs font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
            onClick={(event) => {
              event.stopPropagation();
              data.onResume(data.nodeId);
            }}
            disabled={!!data.resumeDisabledReason}
          >
            Resume
          </button>
          {data.resumeDisabledReason && <p className="mt-1 text-[10px] text-zinc-600">{data.resumeDisabledReason}</p>}
        </div>
      )}
      <Handle type="source" position={Position.Right} className="h-2 w-2 border-zinc-300 bg-zinc-200" />
    </div>
  );

  if (!appearance.diamond) return content;
  return (
    <div className="relative h-full w-full p-3" onClick={() => data.onSelect(data.nodeId)}>
      <div className="absolute inset-2 -z-10 rotate-45 rounded-xl border border-zinc-300 bg-white" />
      <div className="-rotate-0">{content}</div>
    </div>
  );
}

function GroupNodeComponent({ data }: NodeProps<GroupNodeData>) {
  return (
    <div className="h-full w-full rounded-2xl border border-dashed border-zinc-200 bg-zinc-100/60 p-2">
      <span className="rounded bg-zinc-200 px-2 py-0.5 text-[10px] font-semibold text-zinc-700">{data.label}</span>
    </div>
  );
}

const NODE_TYPES: NodeTypes = {
  executionNode: ExecutionNodeComponent,
  groupNode: GroupNodeComponent
};

type NodeGraphViewProps = {
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: PositionedEdge[];
  groups: GroupBounds[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string | null) => void;
  onResumeNode: (nodeId: string) => void;
  getResumeDisabledReason: (nodeId: string) => string | null;
};

export function NodeGraphView({
  nodes,
  edges,
  groups,
  selectedNodeId,
  onSelectNode,
  onResumeNode,
  getResumeDisabledReason
}: NodeGraphViewProps) {
  const graphNodes = useMemo<Array<Node<ExecutionNodeData | GroupNodeData>>>(() => {
    const groupNodes: Array<Node<GroupNodeData>> = groups.map((group) => ({
      id: `group-${group.groupId}`,
      type: "groupNode",
      data: { label: group.label },
      position: { x: group.x, y: group.y },
      style: { width: group.w, height: group.h },
      draggable: false,
      selectable: false
    }));

    const executionNodes: Array<Node<ExecutionNodeData>> = nodes.map((node) => ({
      id: node.nodeId,
      type: "executionNode",
      position: { x: node.x, y: node.y },
      data: {
        nodeId: node.nodeId,
        label: node.nodeId,
        nodeType: node.nodeType,
        status: node.status as NodeStatus,
        attempt: node.attempt,
        waitKey: node.waitKey,
        selected: selectedNodeId === node.nodeId,
        onSelect: (nodeId: string) => onSelectNode(nodeId),
        onResume: (nodeId: string) => onResumeNode(nodeId),
        resumeDisabledReason: getResumeDisabledReason(node.nodeId)
      },
      style: { width: node.w, height: node.h }
    }));

    return [...groupNodes, ...executionNodes];
  }, [groups, nodes, onResumeNode, onSelectNode, getResumeDisabledReason, selectedNodeId]);

  const graphEdges = useMemo<Array<Edge>>(
    () =>
      edges.map((edge) => ({
        id: edge.id,
        source: edge.from,
        target: edge.to,
        markerEnd: { type: MarkerType.ArrowClosed, width: 14, height: 14 },
        animated: false,
        style: { stroke: "#d4d4d8", strokeWidth: 1.2 }
      })),
    [edges]
  );

  return (
    <div className="h-[620px] rounded-2xl border border-zinc-200 bg-white shadow-sm">
      <ReactFlow
        nodes={graphNodes}
        edges={graphEdges}
        nodeTypes={NODE_TYPES}
        onNodeClick={(_, node) => {
          if (!String(node.id).startsWith("group-")) onSelectNode(String(node.id));
        }}
        onPaneClick={() => onSelectNode(null)}
        fitView
        fitViewOptions={{ padding: 0.2, minZoom: 0.2, maxZoom: 1.5 }}
      >
        <MiniMap pannable zoomable />
        <Controls showInteractive />
        <Background gap={20} size={1} color="#e4e4e7" />
      </ReactFlow>
    </div>
  );
}
