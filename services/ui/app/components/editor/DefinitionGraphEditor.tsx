"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { MouseEvent } from "react";
import ReactFlow, {
  Background,
  Controls,
  Handle,
  MarkerType,
  MiniMap,
  Position,
  useNodeId,
  useNodesState,
  useUpdateNodeInternals
} from "reactflow";
import type { Connection, Edge, Node, NodeProps, NodeTypes, OnConnect } from "reactflow";
import "reactflow/dist/style.css";
import { layoutGraph } from "../../lib/graphLayout";
import type { LayoutEdgeInput, LayoutNodeInput } from "../../lib/graphLayout";
import { getNodeAppearance } from "../../lib/nodeAppearance";
import { getStatusStyle } from "../../lib/statusStyle";
import { renameNodeIdInDocument } from "../../lib/definition-editor/renameNodeIdInDocument";
import type { DefinitionGraphDocument, DefinitionGraphNode, NodeType } from "../../lib/definition-editor/types";
import { ActionInputCodeEditor } from "./ActionInputCodeEditor";
import { GraphNodeShell } from "../nodes/GraphNodeShell";

function formatActionInputForEditor(input: DefinitionGraphNode["input"]): string {
  if (input === undefined) {
    return "";
  }
  if (typeof input === "string") {
    return input;
  }
  try {
    return JSON.stringify(input, null, 2);
  } catch {
    return "";
  }
}

/**
 * パス文字列はそのまま、JSON オブジェクトは `{ ... }` として入力する。
 */
function parseActionInputEditorText(text: string): string | Record<string, unknown> | undefined {
  const t = text.trim();
  if (!t) {
    return undefined;
  }
  if (t.startsWith("{") || t.startsWith("[")) {
    const parsed: unknown = JSON.parse(t);
    if (parsed !== null && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    throw new SyntaxError("Input JSON must be a single object for action input mapping.");
  }
  return t;
}

type DefinitionGraphNodeData = {
  nodeType: string;
  label: string;
  /** React Flow の計測・ハンドル位置と一致させる（layoutGraph の w/h と同一） */
  width: number;
  height: number;
};

const handleClassName =
  "z-20 h-4 w-4 border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]";

function DefinitionGraphNodeComponent({ data }: NodeProps<DefinitionGraphNodeData>) {
  const appearance = getNodeAppearance(data.nodeType);
  const chrome = getStatusStyle("IDLE");
  const isGateway = appearance.shapeKind === "gatewayFork" || appearance.shapeKind === "gatewayJoin";
  const flowNodeId = useNodeId();
  const updateInternals = useUpdateNodeInternals();

  const t = data.nodeType.trim().toUpperCase();
  const isStart = t === "START";
  const isEnd = t === "END";
  const isAction = t === "ACTION";
  /** スタート: 出し口（下）のみ。エンド: 受け口（上）のみ。それ以外: 上下とも。 */
  const showTargetHandle = !isStart;
  const showSourceHandle = !isEnd;

  useEffect(() => {
    if (flowNodeId != null && flowNodeId !== "") {
      updateInternals(flowNodeId);
    }
  }, [flowNodeId, updateInternals, data.nodeType, data.label, data.width, data.height, showTargetHandle, showSourceHandle, isAction]);

  return (
    <div
      className={`relative box-border flex min-h-0 flex-col ${isGateway ? "bg-transparent" : ""}`}
      style={{
        width: data.width,
        height: data.height,
        minWidth: data.width,
        minHeight: data.height
      }}
    >
      {showTargetHandle && (
        <Handle
          id="in"
          type="target"
          position={Position.Top}
          className={handleClassName}
        />
      )}
      <div className="relative z-0 flex min-h-0 flex-1 flex-col overflow-hidden">
        <GraphNodeShell
          shapeKind={appearance.shapeKind}
          borderClass={chrome.borderClass}
          bgClass={chrome.bgClass}
          className="h-full min-h-0"
        >
          <div className="flex flex-col gap-0.5">
            <span className="text-[10px] font-semibold">{appearance.label}</span>
            <span className="break-all font-mono text-[9px] leading-tight">{data.label}</span>
          </div>
        </GraphNodeShell>
      </div>
      {showSourceHandle && (
        <Handle
          id="out"
          type="source"
          position={Position.Bottom}
          className={handleClassName}
        />
      )}
      {isAction && (
        <Handle
          id="out-error"
          type="source"
          position={Position.Right}
          className={handleClassName}
        />
      )}
    </div>
  );
}

const DEFINITION_GRAPH_NODE_TYPES: NodeTypes = {
  definitionGraphNode: DefinitionGraphNodeComponent
};

const DEFINITION_GRAPH_EDGE_DEFAULTS = {
  type: "smoothstep" as const,
  className: "definition-graph-edge",
  style: { strokeWidth: 2.75, stroke: "var(--md-sys-color-outline)" },
  markerEnd: {
    type: MarkerType.ArrowClosed,
    width: 15,
    height: 15,
    color: "var(--md-sys-color-outline)"
  },
  labelShowBg: true,
  labelStyle: { fontSize: 10, fontWeight: 600, fill: "var(--md-sys-color-on-surface)" },
  labelBgStyle: { minWidth: 52, fill: "var(--md-sys-color-surface)" },
  labelBgPadding: [4, 4] as [number, number],
  labelBgBorderRadius: 2
};

type GraphSelection =
  | { kind: "node"; nodeId: string }
  | { kind: "edge"; nodeId: string; edgeKind: "next" | "edge" | "error"; edgeIndex?: number }
  | null;

type AvailableNodeType = {
  type: NodeType;
  disabled: boolean;
  reason?: string;
};

type DefinitionGraphEditorProps = {
  document: DefinitionGraphDocument | null;
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  validationMessages: string[];
  labels: {
    title: string;
    empty: string;
    addNode: string;
    addNodeDialogTitle: string;
    addNodeDisabledReasonStart: string;
    addNodeDisabledReasonEnd: string;
    nodeInspectorTitle: string;
    edgeInspectorTitle: string;
    deleteNode: string;
    deleteEdge: string;
    apply: string;
    closeDialog: string;
    selfReferenceRejected: string;
    whenOpPlaceholder: string;
    whenPathPlaceholder: string;
    whenValuePlaceholder: string;
    whenValueDisabledForExists: string;
    whenValueHintIn: string;
    whenValueHintBetween: string;
    fullscreenEnter: string;
    fullscreenExit: string;
    actionInputLabel: string;
    actionErrorLabel: string;
    actionInputPlaceholder: string;
    actionInputHint: string;
    actionInputInvalidJson: string;
  };
};

type GraphEdgeMeta = {
  id: string;
  source: string;
  target: string;
  edgeKind: "next" | "edge" | "branch" | "error";
  edgeIndex?: number;
};

const WHEN_OP_OPTIONS = [
  { value: "EQ", label: "EQ (=)" },
  { value: "NE", label: "NE (!=)" },
  { value: "GT", label: "GT (>)" },
  { value: "GTE", label: "GTE (>=)" },
  { value: "LT", label: "LT (<)" },
  { value: "LTE", label: "LTE (<=)" },
  { value: "EXISTS", label: "EXISTS" },
  { value: "IN", label: "IN" },
  { value: "BETWEEN", label: "BETWEEN" }
] as const;

function toLayoutNodes(document: DefinitionGraphDocument): LayoutNodeInput[] {
  return document.nodes.map((node) => ({
    nodeId: node.id,
    nodeType: node.type.toUpperCase()
  }));
}

function toGraphEdges(document: DefinitionGraphDocument): GraphEdgeMeta[] {
  const edges: GraphEdgeMeta[] = [];
  for (const node of document.nodes) {
    if (node.type === "action" && node.error?.trim()) {
      edges.push({
        id: `error:${node.id}`,
        source: node.id,
        target: node.error.trim(),
        edgeKind: "error"
      });
    }
    if (node.next?.trim()) {
      edges.push({
        id: `next:${node.id}`,
        source: node.id,
        target: node.next.trim(),
        edgeKind: "next"
      });
    }
    for (const [index, edge] of (node.edges ?? []).entries()) {
      if (!edge.to?.trim()) {
        continue;
      }
      edges.push({
        id: `edge:${node.id}:${index}`,
        source: node.id,
        target: edge.to.trim(),
        edgeKind: "edge",
        edgeIndex: index
      });
    }
    for (const [index, branch] of (node.branches ?? []).entries()) {
      if (!branch?.trim()) {
        continue;
      }
      edges.push({
        id: `branch:${node.id}:${index}`,
        source: node.id,
        target: branch.trim(),
        edgeKind: "branch",
        edgeIndex: index
      });
    }
  }
  return edges;
}

function buildAvailableNodeTypes(
  document: DefinitionGraphDocument,
  labels: Pick<DefinitionGraphEditorProps["labels"], "addNodeDisabledReasonStart" | "addNodeDisabledReasonEnd">
): AvailableNodeType[] {
  const startCount = document.nodes.filter((node) => node.type === "start").length;
  const endCount = document.nodes.filter((node) => node.type === "end").length;
  return [
    {
      type: "start",
      disabled: startCount >= 1,
      reason: startCount >= 1 ? labels.addNodeDisabledReasonStart : undefined
    },
    { type: "action", disabled: false },
    { type: "wait", disabled: false },
    { type: "fork", disabled: false },
    { type: "join", disabled: false },
    {
      type: "end",
      disabled: endCount >= 1,
      reason: endCount >= 1 ? labels.addNodeDisabledReasonEnd : undefined
    }
  ];
}

function nextNodeId(document: DefinitionGraphDocument, type: NodeType): string {
  const used = new Set(document.nodes.map((node) => node.id.toLowerCase()));
  for (let index = 1; index < 9999; index += 1) {
    const candidate = `${type}_${index}`;
    if (!used.has(candidate.toLowerCase())) {
      return candidate;
    }
  }
  return `${type}_${crypto.randomUUID().slice(0, 8)}`;
}

function createNode(type: NodeType, id: string): DefinitionGraphNode {
  switch (type) {
    case "start":
      return { id, type: "start" };
    case "action":
      return { id, type: "action", action: "noop" };
    case "wait":
      return { id, type: "wait", event: "resume" };
    case "fork":
      return { id, type: "fork", branches: [] };
    case "join":
      return { id, type: "join" };
    case "end":
      return { id, type: "end" };
  }
}

function updateNode(document: DefinitionGraphDocument, nodeId: string, updater: (node: DefinitionGraphNode) => DefinitionGraphNode): DefinitionGraphDocument {
  return {
    ...document,
    nodes: document.nodes.map((node) => (node.id === nodeId ? updater(node) : node))
  };
}

/** 十進・指数表記の ASCII 数値リテラル風（0x 等は含まない）。when の YAML 往復・パースで共通利用 */
const DECIMAL_NUMERIC_STRING_PATTERN = /^[-+]?(?:\d+\.?\d*|\.\d+)(?:e[-+]?\d+)?$/i;

function formatWhenValue(value: unknown): string {
  if (typeof value === "string") {
    const trimmed = value.trim();
    const asLower = trimmed.toLowerCase();
    const needsQuotesToStayString =
      asLower === "true" || asLower === "false" || DECIMAL_NUMERIC_STRING_PATTERN.test(trimmed);
    if (needsQuotesToStayString) {
      return `"${value}"`;
    }
    return value;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return `${value}`;
  }
  if (value == null) {
    return "";
  }
  try {
    return JSON.stringify(value);
  } catch {
    return "";
  }
}

function parseWhenValueInput(input: string, op?: string): unknown {
  const trimmed = input.trim();
  const upperOp = op?.toUpperCase();

  if ((upperOp === "IN" || upperOp === "BETWEEN") && trimmed.startsWith("[") && trimmed.endsWith("]")) {
    try {
      const parsed = JSON.parse(trimmed);
      if (Array.isArray(parsed)) {
        return parsed;
      }
    } catch {
      // JSON 配列として解釈できない場合は既存ルールへフォールバックする。
    }
  }

  if (trimmed.length >= 2 && trimmed.startsWith("\"") && trimmed.endsWith("\"")) {
    return trimmed.slice(1, -1);
  }

  const normalized = trimmed.toLowerCase();
  if (normalized === "true") {
    return true;
  }
  if (normalized === "false") {
    return false;
  }

  if (DECIMAL_NUMERIC_STRING_PATTERN.test(trimmed)) {
    return Number(trimmed);
  }

  return input;
}

export function DefinitionGraphEditor({
  document,
  onDocumentChange,
  validationMessages,
  labels
}: Readonly<DefinitionGraphEditorProps>) {
  const [selection, setSelection] = useState<GraphSelection>(null);
  const [graphMessage, setGraphMessage] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const [nodes, setNodes, onNodesChange] = useNodesState<DefinitionGraphNodeData>([]);

  const graphLayout = useMemo(() => {
    if (!document) {
      return {
        edges: [] as Edge[],
        edgeMap: new Map<string, GraphEdgeMeta>(),
        layoutById: new Map<string, { x: number; y: number; w: number; h: number }>()
      };
    }
    const sourceEdges = toGraphEdges(document);
    const layout = layoutGraph(
      toLayoutNodes(document),
      sourceEdges.map<LayoutEdgeInput>((edge) => ({
        id: edge.id,
        from: edge.source,
        to: edge.target
      })),
      { defaultNodeSize: { w: 240, h: 64 } }
    );
    const layoutById = new Map(layout.nodes.map((node) => [node.nodeId, node]));
    const edgeMap = new Map(sourceEdges.map((edge) => [edge.id, edge]));
    const edges: Edge[] = sourceEdges.map((edge) => {
      let label = "edge";
      if (edge.edgeKind === "next") {
        label = "next";
      } else if (edge.edgeKind === "error") {
        label = "error";
      } else if (edge.edgeKind === "branch") {
        label = "branch";
      }
      return {
        id: edge.id,
        source: edge.source,
        target: edge.target,
        sourceHandle: edge.edgeKind === "error" ? "out-error" : "out",
        targetHandle: "in",
        label,
        animated: edge.edgeKind === "edge"
      };
    });
    return { edges, edgeMap, layoutById };
  }, [document]);

  useEffect(() => {
    if (!document) {
      setNodes([]);
      return;
    }
    const { layoutById } = graphLayout;
    setNodes((prev) => {
      const prevPos = new Map(prev.map((n) => [n.id, n.position]));
      return document.nodes.map((node) => {
        const positioned = layoutById.get(node.id);
        const pos =
          document.meta?.layout?.[node.id] ?? prevPos.get(node.id) ?? { x: positioned?.x ?? 0, y: positioned?.y ?? 0 };
        const w = positioned?.w ?? 220;
        const h = positioned?.h ?? 120;
        const rfNode: Node<DefinitionGraphNodeData> = {
          id: node.id,
          type: "definitionGraphNode",
          position: pos,
          style: { width: w, height: h },
          width: w,
          height: h,
          sourcePosition: Position.Bottom,
          targetPosition: Position.Top,
          data: {
            nodeType: node.type.toUpperCase(),
            label: node.id,
            width: w,
            height: h
          },
          draggable: true,
          connectable: true
        };
        return rfNode;
      });
    });
  }, [document, graphLayout, setNodes]);

  const persistNodePosition = useCallback(
    (nodeId: string, position: { x: number; y: number }) => {
      if (!document) {
        return;
      }
      onDocumentChange({
        ...document,
        meta: {
          ...document.meta,
          layout: {
            ...document.meta?.layout,
            [nodeId]: { x: position.x, y: position.y }
          }
        }
      });
    },
    [document, onDocumentChange]
  );

  const handleNodeDragStop = useCallback(
    (_event: MouseEvent, node: Node<DefinitionGraphNodeData>) => {
      persistNodePosition(String(node.id), node.position);
    },
    [persistNodePosition]
  );

  const availableNodeTypes = useMemo(
    () => (document ? buildAvailableNodeTypes(document, labels) : []),
    [document, labels]
  );

  const handleConnect: OnConnect = (connection: Connection) => {
    if (!document || !connection.source || !connection.target) {
      return;
    }
    const targetNodeId = connection.target;
    if (connection.source === connection.target) {
      setGraphMessage(labels.selfReferenceRejected);
      return;
    }
    const sourceNode = document.nodes.find((node) => node.id === connection.source);
    if (!sourceNode) {
      return;
    }
    if (connection.sourceHandle === "out-error") {
      if (sourceNode.type !== "action") {
        return;
      }
      onDocumentChange(
        updateNode(document, sourceNode.id, (node) =>
          node.type === "action" ? { ...node, error: targetNodeId } : node
        )
      );
      setGraphMessage(null);
      return;
    }
    const nextDocument = updateNode(document, sourceNode.id, (node) => {
      if (node.type === "fork") {
        const branches = new Set(node.branches ?? []);
        branches.add(targetNodeId);
        return { ...node, branches: Array.from(branches) };
      }
      if (!node.next && (!node.edges || node.edges.length === 0)) {
        return { ...node, next: targetNodeId };
      }
      if (node.next && (!node.edges || node.edges.length === 0)) {
        if (node.next === targetNodeId) {
          return node;
        }
        return {
          ...node,
          next: undefined,
          edges: [{ to: node.next }, { to: targetNodeId }]
        };
      }
      const existing = node.edges ?? [];
      if (existing.some((edge) => edge.to === targetNodeId)) {
        return node;
      }
      return {
        ...node,
        edges: [...existing, { to: targetNodeId }]
      };
    });
    onDocumentChange(nextDocument);
    setGraphMessage(null);
  };

  useEffect(() => {
    if (!isFullscreen) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsFullscreen(false);
      }
    };
    globalThis.addEventListener("keydown", onKeyDown);
    return () => globalThis.removeEventListener("keydown", onKeyDown);
  }, [isFullscreen]);

  if (!document) {
    return (
      <section className="rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4">
        <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">{labels.empty}</p>
      </section>
    );
  }

  const wrapperClassName = isFullscreen ? "fixed inset-0 z-50 bg-[var(--md-sys-color-surface-container-high)] p-4" : "";
  const panelClassName = isFullscreen
    ? "mx-auto h-full w-full max-w-[1600px] space-y-3 rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4"
    : "space-y-3 rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4";
  const gridClassName = isFullscreen
    ? "grid h-[calc(100%-4rem)] gap-3 lg:grid-cols-[minmax(0,1fr)_340px]"
    : "grid gap-3 lg:grid-cols-[minmax(0,1fr)_340px]";
  const graphHeightClassName = isFullscreen ? "h-full min-h-[520px]" : "h-[420px] lg:h-[520px]";

  return (
    <div className={wrapperClassName}>
      <section className={panelClassName}>
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-sm font-semibold text-[var(--md-sys-color-on-surface)]">{labels.title}</h3>
        <button
          type="button"
          className="ml-auto rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-2 py-1 text-xs"
          onClick={() => setIsFullscreen((current) => !current)}
        >
          {isFullscreen ? labels.fullscreenExit : labels.fullscreenEnter}
        </button>
      </div>

      <div className={gridClassName}>
        <div
          className={`${graphHeightClassName} min-h-0 min-w-0 rounded border border-[var(--md-sys-color-outline-variant)]`}
        >
          <ReactFlow
            nodes={nodes}
            edges={graphLayout.edges}
            nodeTypes={DEFINITION_GRAPH_NODE_TYPES}
            onNodesChange={onNodesChange}
            onNodeDragStop={handleNodeDragStop}
            onConnect={handleConnect}
            defaultEdgeOptions={DEFINITION_GRAPH_EDGE_DEFAULTS}
            elevateEdgesOnSelect
            edgesFocusable
            onNodeClick={(_, node) => setSelection({ kind: "node", nodeId: String(node.id) })}
            onEdgeClick={(_, edge) => {
              const meta = graphLayout.edgeMap.get(String(edge.id));
              if (!meta || meta.edgeKind === "branch") {
                return;
              }
              setSelection({
                kind: "edge",
                nodeId: meta.source,
                edgeKind: meta.edgeKind,
                edgeIndex: meta.edgeIndex
              });
            }}
            onPaneClick={() => setSelection(null)}
            fitView
          >
            <MiniMap zoomable pannable />
            <Controls />
            <Background />
          </ReactFlow>
        </div>
        <div className="flex h-full min-h-0 flex-col gap-2 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] p-2">
          <section className="shrink-0 space-y-2 rounded border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-2">
            <p className="text-sm font-medium">{labels.addNodeDialogTitle}</p>
            <div className="grid grid-cols-2 gap-2">
              {availableNodeTypes.map((entry) => (
                <button
                  key={entry.type}
                  type="button"
                  disabled={entry.disabled}
                  className="rounded border border-[var(--md-sys-color-outline)] px-2 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
                  onClick={() => {
                    if (entry.disabled) {
                      return;
                    }
                    const id = nextNodeId(document, entry.type);
                    onDocumentChange({
                      ...document,
                      nodes: [...document.nodes, createNode(entry.type, id)]
                    });
                    setSelection({ kind: "node", nodeId: id });
                  }}
                  title={entry.reason}
                >
                  {entry.type}
                </button>
              ))}
            </div>
            {availableNodeTypes.some((entry) => entry.disabled && entry.reason) && (
              <ul className="list-disc pl-4 text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {availableNodeTypes
                  .filter((entry) => entry.disabled && entry.reason)
                  .map((entry) => (
                    <li key={`${entry.type}-${entry.reason}`}>{entry.reason}</li>
                  ))}
              </ul>
            )}
          </section>
          <div className="min-h-0 flex-1 overflow-y-auto">
            <GraphInspector
              document={document}
              selection={selection}
              labels={labels}
              onDocumentChange={onDocumentChange}
              onClearSelection={() => setSelection(null)}
              onInspectingNodeIdChange={(nextId) => setSelection({ kind: "node", nodeId: nextId })}
            />
          </div>
        </div>
      </div>

      {graphMessage && <p className="text-xs text-rose-600">{graphMessage}</p>}
      {validationMessages.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-xs text-rose-600">
          {validationMessages.slice(0, 6).map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      </section>
    </div>
  );
}

type GraphInspectorProps = {
  document: DefinitionGraphDocument;
  selection: GraphSelection;
  labels: DefinitionGraphEditorProps["labels"];
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  onClearSelection: () => void;
  /** id 入力でノード識別子が変わったとき選択状態を追従させる（未追従だとインスペクターが消える） */
  onInspectingNodeIdChange?: (nextId: string) => void;
};

type GraphNodeInspectorProps = {
  document: DefinitionGraphDocument;
  node: DefinitionGraphNode;
  labels: DefinitionGraphEditorProps["labels"];
  onDocumentChange: (nextDocument: DefinitionGraphDocument) => void;
  onClearSelection: () => void;
  onInspectingNodeIdChange?: (nextId: string) => void;
};

function GraphNodeInspector({
  document,
  node,
  labels,
  onDocumentChange,
  onClearSelection,
  onInspectingNodeIdChange
}: Readonly<GraphNodeInspectorProps>) {
  const actionInputSig = node.type === "action" ? JSON.stringify(node.input ?? null) : "";
  const [actionInputDraft, setActionInputDraft] = useState("");
  const [actionInputError, setActionInputError] = useState<string | null>(null);

  useEffect(() => {
    if (node.type === "action") {
      setActionInputDraft(formatActionInputForEditor(node.input));
      setActionInputError(null);
    }
  }, [node.id, node.type, actionInputSig]);

  return (
    <section className="space-y-2 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] p-3">
      <p className="text-sm font-medium">{labels.nodeInspectorTitle}</p>
      <label className="block text-xs">
        <span className="block">id</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={node.id}
          onChange={(changeEvent) => {
            const nextId = changeEvent.target.value;
            onDocumentChange(renameNodeIdInDocument(document, node.id, nextId));
            onInspectingNodeIdChange?.(nextId);
          }}
        />
      </label>
      {(node.type === "action" || node.type === "wait") && (
        <label className="block text-xs">
          <span className="block">{node.type === "action" ? "action" : "event"}</span>
          <input
            className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
            value={node.type === "action" ? node.action ?? "" : node.event ?? ""}
            onChange={(changeEvent) => {
              const nextValue = changeEvent.target.value;
              onDocumentChange(
                updateNode(document, node.id, (targetNode) =>
                  targetNode.type === "action"
                    ? { ...targetNode, action: nextValue }
                    : { ...targetNode, event: nextValue }
                )
              );
            }}
          />
        </label>
      )}
      {node.type === "action" && (
        <label className="block text-xs">
          <span className="block">{labels.actionErrorLabel}</span>
          <input
            className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
            value={node.error ?? ""}
            onChange={(changeEvent) => {
              const nextValue = changeEvent.target.value.trim();
              onDocumentChange(
                updateNode(document, node.id, (targetNode) =>
                  targetNode.type === "action"
                    ? { ...targetNode, error: nextValue.length > 0 ? nextValue : undefined }
                    : targetNode
                )
              );
            }}
          />
        </label>
      )}
      {node.type === "action" && (
        <label className="block text-xs">
          <span className="block">{labels.actionInputLabel}</span>
          <ActionInputCodeEditor
            key={node.id}
            value={actionInputDraft}
            placeholder={labels.actionInputPlaceholder}
            onChange={(next) => {
              setActionInputDraft(next);
              setActionInputError(null);
            }}
            onBlur={(latestText) => {
              try {
                const parsed = parseActionInputEditorText(latestText);
                setActionInputError(null);
                onDocumentChange(
                  updateNode(document, node.id, (targetNode) =>
                    targetNode.type === "action" ? { ...targetNode, input: parsed } : targetNode
                  )
                );
                setActionInputDraft(formatActionInputForEditor(parsed));
              } catch {
                setActionInputError(labels.actionInputInvalidJson);
              }
            }}
          />
          <span className="mt-0.5 block text-[10px] text-[var(--md-sys-color-on-surface-variant)]">{labels.actionInputHint}</span>
          {actionInputError ? <p className="text-[10px] text-rose-600">{actionInputError}</p> : null}
        </label>
      )}
      {node.type === "fork" && (
        <label className="block text-xs">
          <span className="block">branches (comma separated)</span>
          <input
            className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
            value={(node.branches ?? []).join(", ")}
            onChange={(changeEvent) => {
              const branches = changeEvent.target.value
                .split(",")
                .map((entry) => entry.trim())
                .filter((entry) => entry.length > 0);
              onDocumentChange(updateNode(document, node.id, (targetNode) => ({ ...targetNode, branches })));
            }}
          />
        </label>
      )}
      <div className="flex justify-end">
        <button
          type="button"
          className="rounded border border-rose-400 px-2 py-1 text-xs text-rose-700"
          onClick={() => {
            onDocumentChange({
              ...document,
              nodes: document.nodes.filter((entry) => entry.id !== node.id)
            });
            onClearSelection();
          }}
        >
          {labels.deleteNode}
        </button>
      </div>
    </section>
  );
}

function GraphInspector({
  document,
  selection,
  labels,
  onDocumentChange,
  onClearSelection,
  onInspectingNodeIdChange
}: Readonly<GraphInspectorProps>) {
  if (!selection) {
    return null;
  }

  if (selection.kind === "node") {
    const node = document.nodes.find((entry) => entry.id === selection.nodeId);
    if (!node) {
      return null;
    }
    return (
      <GraphNodeInspector
        document={document}
        node={node}
        labels={labels}
        onDocumentChange={onDocumentChange}
        onClearSelection={onClearSelection}
        onInspectingNodeIdChange={onInspectingNodeIdChange}
      />
    );
  }

  const sourceNode = document.nodes.find((node) => node.id === selection.nodeId);
  if (!sourceNode) {
    return null;
  }

  let targetEdge: NonNullable<DefinitionGraphNode["edges"]>[number] | { to: string } | undefined;
  if (selection.edgeKind === "next") {
    targetEdge = { to: sourceNode.next ?? "" };
  } else if (selection.edgeKind === "error") {
    targetEdge = { to: sourceNode.error ?? "" };
  } else {
    targetEdge = (sourceNode.edges ?? [])[selection.edgeIndex ?? -1];
  }
  if (!targetEdge) {
    return null;
  }

  const conditionalEdge: NonNullable<DefinitionGraphNode["edges"]>[number] | undefined =
    selection.edgeKind === "edge" ? (targetEdge as NonNullable<DefinitionGraphNode["edges"]>[number]) : undefined;
  const selectedWhenOp = (conditionalEdge?.when?.op ?? "").toUpperCase();
  const isDefaultEdge = conditionalEdge?.default === true;
  const isWhenFieldsDisabled = isDefaultEdge;
  const isWhenValueDisabled = selectedWhenOp === "EXISTS";
  let whenValueHint: string | null = null;
  if (selectedWhenOp === "IN") {
    whenValueHint = labels.whenValueHintIn;
  } else if (selectedWhenOp === "BETWEEN") {
    whenValueHint = labels.whenValueHintBetween;
  }

  return (
    <section className="space-y-2 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] p-3">
      <p className="text-sm font-medium">{labels.edgeInspectorTitle}</p>
      <label className="block text-xs">
        <span className="block">to</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={targetEdge.to}
          onChange={(changeEvent) => {
            const nextTarget = changeEvent.target.value;
            if (selection.edgeKind === "next") {
              onDocumentChange(updateNode(document, sourceNode.id, (node) => ({ ...node, next: nextTarget })));
              return;
            }
            if (selection.edgeKind === "error") {
              onDocumentChange(
                updateNode(document, sourceNode.id, (node) =>
                  node.type === "action" ? { ...node, error: nextTarget.trim() || undefined } : node
                )
              );
              return;
            }
            onDocumentChange(
              updateNode(document, sourceNode.id, (node) => ({
                ...node,
                edges: (node.edges ?? []).map((edge, index) =>
                  index === selection.edgeIndex ? { ...edge, to: nextTarget } : edge
                )
              }))
            );
          }}
        />
      </label>
      {selection.edgeKind === "edge" && (
        <div className="space-y-2">
          <label className="inline-flex items-center gap-2 text-xs">
            <input
              type="checkbox"
                checked={conditionalEdge?.default === true}
              onChange={(changeEvent) => {
                const isDefault = changeEvent.target.checked;
                onDocumentChange(
                  updateNode(document, sourceNode.id, (node) => ({
                    ...node,
                    edges: (node.edges ?? []).map((edge, index) =>
                      index === selection.edgeIndex
                        ? {
                            ...edge,
                            default: isDefault ? true : undefined,
                            ...(isDefault ? { when: undefined } : {})
                          }
                        : edge
                    )
                  }))
                );
              }}
            />
            <span>default</span>
          </label>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          <label className="block text-xs">
            <span className="block">when.path</span>
            <input
              className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
              value={conditionalEdge?.when?.path ?? ""}
              placeholder={labels.whenPathPlaceholder}
              disabled={isWhenFieldsDisabled}
              onChange={(changeEvent) => {
                const path = changeEvent.target.value;
                onDocumentChange(
                  updateNode(document, sourceNode.id, (node) => ({
                    ...node,
                    edges: (node.edges ?? []).map((edge, index) =>
                      index === selection.edgeIndex
                        ? { ...edge, when: { path, op: edge.when?.op ?? "eq", value: edge.when?.value ?? "" } }
                        : edge
                    )
                  }))
                );
              }}
            />
          </label>
          <label className="block text-xs">
            <span className="block">when.op</span>
            <select
              className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
              value={selectedWhenOp}
              disabled={isWhenFieldsDisabled}
              onChange={(changeEvent) => {
                const op = changeEvent.target.value.toUpperCase();
                onDocumentChange(
                  updateNode(document, sourceNode.id, (node) => ({
                    ...node,
                    edges: (node.edges ?? []).map((edge, index) =>
                      index === selection.edgeIndex
                        ? {
                            ...edge,
                            when: {
                              path: edge.when?.path ?? "$.x",
                              op,
                              value: op === "EXISTS" ? undefined : (edge.when?.value ?? "")
                            }
                          }
                        : edge
                    )
                  }))
                );
              }}
            >
              <option value="" disabled>
                {labels.whenOpPlaceholder}
              </option>
              {WHEN_OP_OPTIONS.map((op) => (
                <option key={op.value} value={op.value}>
                  {op.label}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-xs">
            <span className="block">when.value</span>
            <input
              className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
              value={formatWhenValue(conditionalEdge?.when?.value)}
              placeholder={labels.whenValuePlaceholder}
              disabled={isWhenFieldsDisabled || isWhenValueDisabled}
              onChange={(changeEvent) => {
                const value = parseWhenValueInput(changeEvent.target.value, selectedWhenOp);
                onDocumentChange(
                  updateNode(document, sourceNode.id, (node) => ({
                    ...node,
                    edges: (node.edges ?? []).map((edge, index) =>
                      index === selection.edgeIndex
                        ? { ...edge, when: { path: edge.when?.path ?? "$.x", op: edge.when?.op ?? "eq", value } }
                        : edge
                    )
                  }))
                );
              }}
            />
            {!isWhenFieldsDisabled && isWhenValueDisabled && (
              <span className="mt-1 block text-[11px] text-[var(--md-sys-color-on-surface-variant)]">
                {labels.whenValueDisabledForExists}
              </span>
            )}
            {!isWhenFieldsDisabled && !isWhenValueDisabled && whenValueHint && (
              <span className="mt-1 block text-[11px] text-[var(--md-sys-color-on-surface-variant)]">
                {whenValueHint}
              </span>
            )}
          </label>
        </div>
        </div>
      )}
      <div className="flex justify-end">
        <button
          type="button"
          className="rounded border border-rose-400 px-2 py-1 text-xs text-rose-700"
          onClick={() => {
            if (selection.edgeKind === "next") {
              onDocumentChange(updateNode(document, sourceNode.id, (node) => ({ ...node, next: undefined })));
              onClearSelection();
              return;
            }
            if (selection.edgeKind === "error") {
              onDocumentChange(
                updateNode(document, sourceNode.id, (node) =>
                  node.type === "action" ? { ...node, error: undefined } : node
                )
              );
              onClearSelection();
              return;
            }
            onDocumentChange(
              updateNode(document, sourceNode.id, (node) => ({
                ...node,
                edges: (node.edges ?? []).filter((_, index) => index !== selection.edgeIndex)
              }))
            );
            onClearSelection();
          }}
        >
          {labels.deleteEdge}
        </button>
      </div>
    </section>
  );
}
