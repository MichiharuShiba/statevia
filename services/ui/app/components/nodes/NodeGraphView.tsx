"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import ReactFlow, {
  applyNodeChanges,
  Background,
  Controls,
  Handle,
  MiniMap,
  Position,
  useNodeId,
  useUpdateNodeInternals,
  type Node,
  type NodeChange,
  type NodeProps,
  type NodeTypes,
  type Viewport
} from "reactflow";
import "reactflow/dist/style.css";
import { buildGraphEdges } from "../../lib/buildGraphEdges";
import type { GroupBounds } from "../../lib/grouping";
import type { LayoutEdgeInput, PositionedNode } from "../../lib/graphLayout";
import type { MergedGraphNode } from "../../lib/mergeGraph";
import { getNodeAppearance } from "../../lib/nodeAppearance";
import { getStatusStyle } from "../../lib/statusStyle";
import type { NodeStatus } from "../../lib/types";
import { useUiText } from "../../lib/uiTextContext";
import { GraphLegend } from "./GraphLegend";
import { GraphNodeShell } from "./GraphNodeShell";

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

function isExecutionNodeData(data: ExecutionNodeData | GroupNodeData | undefined): data is ExecutionNodeData {
  return data != null && typeof data === "object" && "nodeId" in data;
}

function ExecutionNodeComponent({ data }: NodeProps<ExecutionNodeData>) {
  const uiText = useUiText();
  const style = getStatusStyle(data.status);
  const appearance = getNodeAppearance(data.nodeType);
  const isRunning = data.status === "RUNNING";
  const isGateway = appearance.shapeKind === "gatewayFork" || appearance.shapeKind === "gatewayJoin";
  const flowNodeId = useNodeId();
  const updateInternals = useUpdateNodeInternals();

  useEffect(() => {
    if (flowNodeId != null && flowNodeId !== "") {
      updateInternals(flowNodeId);
    }
  }, [flowNodeId, updateInternals, data.nodeType, data.status, data.label]);
  let diffRing = "";
  if (data.diffHighlight != null) {
    diffRing =
      data.diffHighlight.isFailureOrCancel === true
        ? "ring-2 ring-red-500 ring-offset-1"
        : "ring-2 ring-amber-400 ring-offset-1";
  }

  const nodeMainSection = (
    <>
      <div className="flex items-center justify-between gap-1 text-xs">
        <span aria-hidden>{appearance.icon}</span>
        <span className="min-w-0 shrink font-semibold">{appearance.label}</span>
        <span className={`inline-flex shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold ${style.badgeClass}`}>{data.status}</span>
      </div>
      <div className={`space-y-1 text-xs ${isGateway ? "mt-1" : "mt-2"}`}>
        <div className="break-all font-mono">{data.label}</div>
        <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.type(data.nodeType)}</div>
        <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.attempt(data.attempt)}</div>
        {data.waitKey && <div className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeGraph.meta.waitKey(data.waitKey)}</div>}
      </div>
    </>
  );

  const waitingResumeSection =
    data.status === "WAITING" ? (
      <div className={`shrink-0 ${isGateway ? "mt-2" : "mt-3"}`}>
        <button
          type="button"
          className="nodrag w-full cursor-pointer rounded-lg bg-amber-500 px-2 py-1.5 text-xs font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
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
    ) : null;

  return (
    <div className={`relative h-full w-full ${isGateway ? "border-0 bg-transparent p-0" : ""}`}>
      <Handle
        id="in"
        type="target"
        position={Position.Top}
        className="h-2 w-2 border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]"
      />
      <GraphNodeShell
        shapeKind={appearance.shapeKind}
        borderClass={style.borderClass}
        bgClass={style.bgClass}
        selected={data.selected}
        diffRing={diffRing}
        isRunning={isRunning}
      >
        <div className="flex h-full min-h-0 flex-col">
          <button
            type="button"
            aria-label={uiText.nodeGraph.aria.selectNode(data.label)}
            className={`min-h-0 flex-1 cursor-grab text-left outline-none ring-offset-2 focus-visible:ring-2 focus-visible:ring-[var(--md-sys-color-primary)] active:cursor-grabbing ${isGateway ? "border-0 bg-transparent p-0" : ""}`}
            onClick={() => data.onSelect(data.nodeId)}
          >
            {nodeMainSection}
          </button>
          {waitingResumeSection}
        </div>
      </GraphNodeShell>
      <Handle
        id="out"
        type="source"
        position={Position.Bottom}
        className="h-2 w-2 border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]"
      />
    </div>
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

/** レイアウト更新時もユーザーがドラッグした実行ノードの座標を維持する */
function mergeLayoutWithPositions(prev: Node[], layout: Node[]): Node[] {
  if (layout.length === 0) return [];
  if (prev.length === 0) return layout;

  const positionById = new Map<string, { x: number; y: number }>();
  for (const n of prev) {
    if (String(n.id).startsWith("group-")) continue;
    if (n.position) positionById.set(n.id, n.position);
  }

  return layout.map((n) => {
    if (String(n.id).startsWith("group-")) return n;
    const kept = positionById.get(n.id);
    if (kept != null && n.type === "executionNode") {
      return { ...n, position: kept };
    }
    return n;
  });
}

export type GraphViewport = Viewport;

type NodeGraphViewProps = {
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: LayoutEdgeInput[];
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
  const layoutNodes = useMemo<Array<Node<ExecutionNodeData | GroupNodeData>>>(() => {
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
      width: node.w,
      height: node.h,
      sourcePosition: Position.Bottom,
      targetPosition: Position.Top,
      draggable: true,
      data: {
        nodeId: node.nodeId,
        label: node.nodeId,
        nodeType: node.nodeType,
        status: node.status,
        attempt: node.attempt,
        waitKey: node.waitKey,
        selected: false,
        onSelect: (nodeId: string) => onSelectNode(nodeId),
        onResume: (nodeId: string) => onResumeNode(nodeId),
        resumeDisabledReason: getResumeDisabledReason(node.nodeId),
        diffHighlight: nodeDiffHighlight?.[node.nodeId] ?? null
      },
      style: { width: node.w, height: node.h }
    }));

    return [...groupNodes, ...executionNodes];
  }, [groups, nodes, onResumeNode, onSelectNode, getResumeDisabledReason, nodeDiffHighlight]);

  const [rfNodes, setRfNodes] = useState<Array<Node<ExecutionNodeData | GroupNodeData>>>([]);

  useEffect(() => {
    setRfNodes((prev) => mergeLayoutWithPositions(prev, layoutNodes));
  }, [layoutNodes]);

  const onNodesChange = useCallback((changes: NodeChange[]) => {
    setRfNodes((nds) => applyNodeChanges(changes, nds));
  }, []);

  const syncedNodes = rfNodes.length > 0 ? rfNodes : layoutNodes;

  const displayNodes = useMemo(() => {
    return syncedNodes.map((node) => {
      if (node.type !== "executionNode") return node;
      if (!isExecutionNodeData(node.data)) return node;
      const d = node.data;
      return {
        ...node,
        data: {
          ...d,
          selected: selectedNodeId === d.nodeId,
          resumeDisabledReason: getResumeDisabledReason(d.nodeId),
          onSelect: (id: string) => onSelectNode(id),
          onResume: (id: string) => onResumeNode(id),
          diffHighlight: nodeDiffHighlight?.[d.nodeId] ?? null
        }
      };
    });
  }, [syncedNodes, selectedNodeId, onSelectNode, onResumeNode, getResumeDisabledReason, nodeDiffHighlight]);

  const graphEdges = useMemo(() => buildGraphEdges(edges), [edges]);

  const graphHeightClass = heightClassName ?? "h-[620px]";

  return (
    <div className={`relative ${graphHeightClass} overflow-hidden rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] shadow-sm`}>
      <ReactFlow
        nodes={displayNodes}
        edges={graphEdges}
        nodeTypes={NODE_TYPES}
        nodesDraggable
        defaultViewport={defaultViewport}
        onNodesChange={onNodesChange}
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
