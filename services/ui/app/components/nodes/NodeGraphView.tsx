"use client";

import { useMemo } from "react";
import ReactFlow, {
  Background,
  Controls,
  Handle,
  MiniMap,
  Position,
  type Node,
  type NodeProps,
  type NodeTypes,
  type Viewport
} from "reactflow";
import "reactflow/dist/style.css";
import { buildGraphEdges } from "../../lib/buildGraphEdges";
import type { GroupBounds } from "../../lib/grouping";
import type { PositionedEdge, PositionedNode } from "../../lib/graphLayout";
import type { MergedGraphNode } from "../../lib/mergeGraph";
import { getNodeAppearance } from "../../lib/nodeAppearance";
import { getStatusStyle } from "../../lib/statusStyle";
import type { NodeStatus } from "../../lib/types";
import { useUiText } from "../../lib/uiTextContext";
import { GraphLegend } from "./GraphLegend";

export type NodeDiffHighlight = Record<string, { isFailureOrCancel: boolean }>;

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
  /** 比較モード時の差分ハイライト（該当時のみ ring 表示） */
  diffHighlight?: { isFailureOrCancel: boolean } | null;
};

type GroupNodeData = {
  label: string;
};

function ExecutionNodeComponent({ data }: NodeProps<ExecutionNodeData>) {
  const uiText = useUiText();
  const style = getStatusStyle(data.status);
  const appearance = getNodeAppearance(data.nodeType);
  const isRunning = data.status === "RUNNING";
  const isFork = appearance.label === "FORK";
  const isJoin = appearance.label === "JOIN";
  let diffRing = "";
  if (data.diffHighlight != null) {
    diffRing =
      data.diffHighlight.isFailureOrCancel === true
        ? "ring-2 ring-red-500 ring-offset-1"
        : "ring-2 ring-amber-400 ring-offset-1";
  }
  const wrapClass = `${appearance.shapeClass} relative border-2 p-3 shadow-sm ${style.borderClass} ${style.bgClass} ${isRunning ? "opacity-80 text-[var(--md-sys-color-on-surface-variant)]" : ""} ${data.selected ? "outline outline-2 outline-[var(--md-sys-color-primary)]" : ""} ${diffRing}`;

  return (
    <button
      type="button"
      className={`w-full text-left ${wrapClass}`}
      onClick={() => data.onSelect(data.nodeId)}
    >
      {isFork && <div className="absolute inset-x-0 top-0 h-3 rounded-t-2xl bg-black/10 dark:bg-white/10" />}
      {isJoin && <div className="absolute inset-x-0 bottom-0 h-3 rounded-b-2xl bg-black/10 dark:bg-white/10" />}
      <Handle type="target" position={Position.Left} className="h-2 w-2 border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]" />
      <div className={`flex items-center justify-between gap-2 text-xs ${isFork ? "pt-1.5" : ""} ${isJoin ? "pb-1.5" : ""}`}>
        <span>{appearance.icon}</span>
        <span className="font-semibold">{appearance.label}</span>
        <span className={`inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold ${style.badgeClass}`}>{data.status}</span>
      </div>
      <div className="mt-2 space-y-1 text-xs">
        <div className="font-mono">{data.label}</div>
        <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.type(data.nodeType)}</div>
        <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.attempt(data.attempt)}</div>
        {data.waitKey && <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.waitKey(data.waitKey)}</div>}
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
            {uiText.actions.resume}
          </button>
          {data.resumeDisabledReason && <p className="mt-1 text-[10px] text-[var(--md-sys-color-on-surface-variant)]">{data.resumeDisabledReason}</p>}
        </div>
      )}
      <Handle type="source" position={Position.Right} className="h-2 w-2 border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]" />
    </button>
  );
}

function GroupNodeComponent({ data }: NodeProps<GroupNodeData>) {
  return (
    <div className="h-full w-full rounded-2xl border border-dashed border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface-container)]/70 p-2">
      <span className="rounded bg-[var(--md-sys-color-surface-container-high)] px-2 py-0.5 text-[10px] font-semibold text-[var(--md-sys-color-on-surface)]">{data.label}</span>
    </div>
  );
}

const NODE_TYPES: NodeTypes = {
  executionNode: ExecutionNodeComponent,
  groupNode: GroupNodeComponent
};

export type GraphViewport = Viewport;

type NodeGraphViewProps = {
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: PositionedEdge[];
  groups: GroupBounds[];
  selectedNodeId: string | null;
  onSelectNode: (nodeId: string | null) => void;
  onResumeNode: (nodeId: string) => void;
  getResumeDisabledReason: (nodeId: string) => string | null;
  heightClassName?: string;
  /** 復元する viewport（指定時は fitView を行わずこの位置・ズームで表示） */
  defaultViewport?: GraphViewport;
  /** パン・ズーム終了時に呼ばれる（状態保持用） */
  onViewportChange?: (viewport: GraphViewport) => void;
  /** 比較モード時のノード差分ハイライト（nodeId -> ハイライト情報） */
  nodeDiffHighlight?: NodeDiffHighlight;
};

export function NodeGraphView({
  nodes,
  edges,
  groups,
  selectedNodeId,
  onSelectNode,
  onResumeNode,
  getResumeDisabledReason,
  heightClassName,
  defaultViewport,
  onViewportChange,
  nodeDiffHighlight
}: Readonly<NodeGraphViewProps>) {
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
        status: node.status,
        attempt: node.attempt,
        waitKey: node.waitKey,
        selected: selectedNodeId === node.nodeId,
        onSelect: (nodeId: string) => onSelectNode(nodeId),
        onResume: (nodeId: string) => onResumeNode(nodeId),
        resumeDisabledReason: getResumeDisabledReason(node.nodeId),
        diffHighlight: nodeDiffHighlight?.[node.nodeId] ?? null
      },
      style: { width: node.w, height: node.h }
    }));

    return [...groupNodes, ...executionNodes];
  }, [groups, nodes, onResumeNode, onSelectNode, getResumeDisabledReason, selectedNodeId, nodeDiffHighlight]);

  const graphEdges = useMemo(() => buildGraphEdges(edges), [edges]);

  const graphHeightClass = heightClassName ?? "h-[620px]";

  return (
    <div className={`relative ${graphHeightClass} overflow-hidden rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] shadow-sm`}>
      <ReactFlow
        nodes={graphNodes}
        edges={graphEdges}
        nodeTypes={NODE_TYPES}
        defaultViewport={defaultViewport}
        onNodeClick={(_, node) => {
          if (!String(node.id).startsWith("group-")) onSelectNode(String(node.id));
        }}
        onPaneClick={() => onSelectNode(null)}
        onMoveEnd={(_event, viewport) => onViewportChange?.(viewport)}
        fitView={defaultViewport == null}
        fitViewOptions={{ padding: 0.2, minZoom: 0.2, maxZoom: 1.5 }}
      >
        <MiniMap pannable zoomable />
        <Controls showInteractive />
        <Background gap={20} size={1} color="#e4e4e7" />
      </ReactFlow>
      <GraphLegend />
    </div>
  );
}
