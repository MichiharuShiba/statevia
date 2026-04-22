"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { ExecutionComparisonBar } from "./ExecutionComparisonBar";
import { ExecutionHeader } from "./ExecutionHeader";
import { ExecutionStatusBanner } from "./ExecutionStatusBanner";
import { ExecutionTimeline } from "./ExecutionTimeline";
import { ReplayBanner } from "./ReplayBanner";
import { TenantMissingBanner } from "./TenantMissingBanner";
import { NodeDetail } from "../nodes/NodeDetail";
import { NodeGraphView, type GraphViewport } from "../nodes/NodeGraphView";
import { NodeListView } from "../nodes/NodeListView";
import { Toast } from "../Toast";
import type { ViewMode } from "../ViewToggle";
import { useExecution } from "../../features/execution/useExecution";
import { useExecutionEvents } from "../../features/execution/useExecutionEvents";
import { useExecutionStateAtSeq } from "../../features/execution/useExecutionStateAtSeq";
import { useGraphDefinition } from "../../features/graph/useGraphDefinition";
import { getNodeWithFallback, useGraphData } from "../../features/graph/useGraphData";
import { getResumeDisabledReason, useNodeCommands } from "../../features/nodes/useNodeCommands";
import { computeExecutionDiff } from "../../lib/executionDiff";
import { apiGet } from "../../lib/api";
import { toToastError, type ToastState } from "../../lib/errors";
import { buildWorkflowView } from "../../lib/workflowView";
import type { WorkflowDTO, WorkflowGraphDTO, WorkflowView } from "../../lib/types";

/** executionId ごとの Graph ビューポート（ズーム・パン位置） */
type GraphViewportByExecutionId = Record<string, GraphViewport>;

const STREAM_PREF_STORAGE_KEY = "statevia.execution.streamEnabled";

export type ExecutionDashboardProps = {
  /** 初期の実行 ID（URL から渡す場合は key と併用） */
  initialExecutionId: string;
  /** true のときマウント直後に Load を実行する */
  autoLoadOnMount?: boolean;
  /** ヘッダ右側のナビ（未指定時は Playground + health） */
  headerNav?: ReactNode;
  /** メイン見出し */
  headerTitle?: string;
};

type ExecutionDashboardViewProps = {
  graphFullscreen: boolean;
  headerTitle: string;
  headerNav?: ReactNode;
  toast: ToastState | null;
  onCloseToast: () => void;
  executionId: string;
  onExecutionIdChange: (executionId: string) => void;
  onLoadExecution: () => void;
  onCancelExecution: () => void;
  loading: boolean;
  canCancel: boolean;
  execution: WorkflowView | null;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  compareMode: boolean;
  onCompareModeChange: (compareMode: boolean) => void;
  streamEnabled: boolean;
  onStreamEnabledChange: (enabled: boolean) => void;
  showExecutionPanels: boolean;
  executionB: WorkflowView | null;
  executionIdB: string;
  onExecutionIdBChange: (executionId: string) => void;
  onLoadExecutionB: () => void;
  loadingB: boolean;
  executionDiff: ReturnType<typeof computeExecutionDiff>;
  onSelectNode: (nodeId: string | null) => void;
  terminal: boolean;
  isReplaying: boolean;
  onBackToCurrent: () => void;
  timelineEvents: ReturnType<typeof useExecutionEvents>["events"];
  timelineLoading: boolean;
  timelineError: ReturnType<typeof useExecutionEvents>["error"];
  replayAtSeq: number | null;
  onSelectSeq: (seq: number | null) => void;
  timelineHasMore: boolean;
  timelineLoadingMore: boolean;
  onTimelineLoadMore: () => void;
  displayExecution: WorkflowView | null;
  selectedNodeId: string | null;
  graphData: ReturnType<typeof useGraphData>;
  onToggleGraphFullscreen: () => void;
  onResumeNode: (nodeId: string) => void;
  getResumeDisabledReasonForNode: (nodeId: string) => string | null;
  savedGraphViewport?: GraphViewport;
  onGraphViewportChange: (viewport: GraphViewport) => void;
  selectedNode: ReturnType<typeof getNodeWithFallback>;
  selectedResumeDisabledReason: string | null;
  resumeEventName: string | null;
};

/**
 * 実行一覧・グラフ・タイムライン・ノード操作の共通ダッシュボード。
 * `/dashboard` や `/playground/run/[displayId]` から利用する。
 */
export function ExecutionDashboard({
  initialExecutionId,
  autoLoadOnMount = false,
  headerNav,
  headerTitle = "実行の詳細"
}: Readonly<ExecutionDashboardProps>) {
  const [executionId, setExecutionId] = useState(initialExecutionId);
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [graphFullscreen, setGraphFullscreen] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [graphViewportByExecutionId, setGraphViewportByExecutionId] = useState<GraphViewportByExecutionId>({});
  const [replayAtSeq, setReplayAtSeq] = useState<number | null>(null);
  const [compareMode, setCompareMode] = useState(false);
  const [executionIdB, setExecutionIdB] = useState("");
  const [executionB, setExecutionB] = useState<WorkflowView | null>(null);
  const [loadingB, setLoadingB] = useState(false);
  const [streamEnabled, setStreamEnabled] = useState(true);

  useEffect(() => {
    setExecutionId(initialExecutionId);
  }, [initialExecutionId]);

  useEffect(() => {
    try {
      const raw = sessionStorage.getItem(STREAM_PREF_STORAGE_KEY);
      if (raw === "0") setStreamEnabled(false);
      else if (raw === "1") setStreamEnabled(true);
    } catch {
      // sessionStorage 不可時は既定のまま
    }
  }, []);

  const handleStreamEnabledChange = useCallback((enabled: boolean) => {
    setStreamEnabled(enabled);
    try {
      sessionStorage.setItem(STREAM_PREF_STORAGE_KEY, enabled ? "1" : "0");
    } catch {
      // ignore
    }
  }, []);

  const executionHookOptions = useMemo(
    () => ({
      onError: (err: unknown) => setToast(toToastError(err)),
      onCancelSuccess: () => setToast({ tone: "success", message: "CancelExecution accepted" }),
      streamEnabled
    }),
    [streamEnabled]
  );

  const {
    execution,
    loading: executionLoading,
    canCancel,
    terminal,
    loadExecution,
    cancelExecution,
    selectedNodeId,
    setSelectedNodeId
  } = useExecution(executionId, executionHookOptions);

  const loadExecutionRef = useRef(loadExecution);
  loadExecutionRef.current = loadExecution;
  const didAutoLoadRef = useRef(false);

  useEffect(() => {
    if (!autoLoadOnMount) return;
    didAutoLoadRef.current = false;
  }, [initialExecutionId, autoLoadOnMount]);

  useEffect(() => {
    if (!autoLoadOnMount || !executionId.trim() || didAutoLoadRef.current) return;
    didAutoLoadRef.current = true;
    void loadExecutionRef.current();
  }, [autoLoadOnMount, executionId]);

  const {
    events: timelineEvents,
    hasMore: timelineHasMore,
    loading: timelineLoading,
    loadingMore: timelineLoadingMore,
    error: timelineError,
    loadMore: timelineLoadMore
  } = useExecutionEvents(execution?.displayId ?? null);
  const { state: stateAtSeq, loading: stateAtSeqLoading } = useExecutionStateAtSeq(
    execution?.displayId ?? null,
    replayAtSeq
  );

  const displayExecution =
    replayAtSeq != null && stateAtSeq != null ? stateAtSeq : execution;
  const isReplaying = replayAtSeq != null && stateAtSeq != null;

  const { definition: graphDefinition, loading: graphDefinitionLoading } = useGraphDefinition(
    execution?.graphId ?? null
  );
  const graphData = useGraphData(displayExecution, graphDefinition);

  const { resumeNode, loading: nodeLoading } = useNodeCommands(execution, {
    onSuccess: () => {
      setToast({ tone: "success", message: "ResumeNode accepted" });
      loadExecution();
    },
    onError: (err) => setToast(toToastError(err))
  });

  const loading =
    executionLoading || nodeLoading || stateAtSeqLoading || graphDefinitionLoading;

  const loadExecutionB = useCallback(async () => {
    if (!executionIdB.trim()) return;
    setLoadingB(true);
    try {
      const workflow = await apiGet<WorkflowDTO>(`/workflows/${executionIdB.trim()}`);
      let graph: WorkflowGraphDTO | null = null;
      try {
        graph = await apiGet<WorkflowGraphDTO>(`/workflows/${executionIdB.trim()}/graph`);
      } catch {
        // ignore
      }
      setExecutionB(buildWorkflowView(workflow, graph));
    } catch {
      setExecutionB(null);
    } finally {
      setLoadingB(false);
    }
  }, [executionIdB]);

  const executionDiff = useMemo(
    () => computeExecutionDiff(execution, executionB),
    [execution, executionB]
  );

  const selectedNode = useMemo(
    () => getNodeWithFallback(displayExecution, graphData, selectedNodeId),
    [displayExecution, graphData, selectedNodeId]
  );

  const selectedResumeDisabledReason = isReplaying
    ? "リプレイ表示中は実行できません"
    : getResumeDisabledReason(execution, selectedNode);

  const resumeEventName = useMemo(() => {
    if (!selectedNodeId || !graphData?.edges) return null;
    const resumeEdge = graphData.edges.find(
      (e) => e.from === selectedNodeId && e.edgeType === "Resume"
    );
    return resumeEdge?.eventName ?? null;
  }, [selectedNodeId, graphData?.edges]);

  const savedGraphViewport = execution ? graphViewportByExecutionId[execution.displayId] : undefined;
  const handleGraphViewportChange = useCallback(
    (viewport: GraphViewport) => {
      if (displayExecution) {
        setGraphViewportByExecutionId((prev) => ({
          ...prev,
          [displayExecution.displayId]: viewport
        }));
      }
    },
    [displayExecution]
  );

  useEffect(() => {
    if (viewMode !== "graph" || !execution) setGraphFullscreen(false);
  }, [viewMode, execution]);

  useEffect(() => {
    if (!graphFullscreen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setGraphFullscreen(false);
    };
    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    globalThis.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = originalOverflow;
      globalThis.removeEventListener("keydown", onKeyDown);
    };
  }, [graphFullscreen]);

  const showExecutionPanels = !!execution;
  const getResumeDisabledReasonForNode = useCallback(
    (nodeId: string) => {
      const node = getNodeWithFallback(displayExecution, graphData, nodeId);
      return isReplaying
        ? "リプレイ表示中は実行できません"
        : getResumeDisabledReason(execution, node);
    },
    [displayExecution, graphData, isReplaying, execution]
  );

  const handleToggleGraphFullscreen = useCallback(() => {
    setGraphFullscreen((value) => !value);
  }, []);

  const handleCloseToast = useCallback(() => {
    setToast(null);
  }, []);

  const handleBackToCurrent = useCallback(() => {
    setReplayAtSeq(null);
  }, []);

  return (
    <ExecutionDashboardView
      graphFullscreen={graphFullscreen}
      headerTitle={headerTitle}
      headerNav={headerNav}
      toast={toast}
      onCloseToast={handleCloseToast}
      executionId={executionId}
      onExecutionIdChange={setExecutionId}
      onLoadExecution={loadExecution}
      onCancelExecution={cancelExecution}
      loading={loading}
      canCancel={canCancel}
      execution={execution}
      viewMode={viewMode}
      onViewModeChange={setViewMode}
      compareMode={compareMode}
      onCompareModeChange={setCompareMode}
      streamEnabled={streamEnabled}
      onStreamEnabledChange={handleStreamEnabledChange}
      showExecutionPanels={showExecutionPanels}
      executionB={executionB}
      executionIdB={executionIdB}
      onExecutionIdBChange={setExecutionIdB}
      onLoadExecutionB={loadExecutionB}
      loadingB={loadingB}
      executionDiff={executionDiff}
      onSelectNode={setSelectedNodeId}
      terminal={terminal}
      isReplaying={isReplaying}
      onBackToCurrent={handleBackToCurrent}
      timelineEvents={timelineEvents}
      timelineLoading={timelineLoading}
      timelineError={timelineError}
      replayAtSeq={replayAtSeq}
      onSelectSeq={setReplayAtSeq}
      timelineHasMore={timelineHasMore}
      timelineLoadingMore={timelineLoadingMore}
      onTimelineLoadMore={timelineLoadMore}
      displayExecution={displayExecution}
      selectedNodeId={selectedNodeId}
      graphData={graphData}
      onToggleGraphFullscreen={handleToggleGraphFullscreen}
      onResumeNode={resumeNode}
      getResumeDisabledReasonForNode={getResumeDisabledReasonForNode}
      savedGraphViewport={savedGraphViewport}
      onGraphViewportChange={handleGraphViewportChange}
      selectedNode={selectedNode}
      selectedResumeDisabledReason={selectedResumeDisabledReason}
      resumeEventName={resumeEventName}
    />
  );
}

function ExecutionDashboardView({
  graphFullscreen,
  headerTitle,
  headerNav,
  toast,
  onCloseToast,
  executionId,
  onExecutionIdChange,
  onLoadExecution,
  onCancelExecution,
  loading,
  canCancel,
  execution,
  viewMode,
  onViewModeChange,
  compareMode,
  onCompareModeChange,
  streamEnabled,
  onStreamEnabledChange,
  showExecutionPanels,
  executionB,
  executionIdB,
  onExecutionIdBChange,
  onLoadExecutionB,
  loadingB,
  executionDiff,
  onSelectNode,
  terminal,
  isReplaying,
  onBackToCurrent,
  timelineEvents,
  timelineLoading,
  timelineError,
  replayAtSeq,
  onSelectSeq,
  timelineHasMore,
  timelineLoadingMore,
  onTimelineLoadMore,
  displayExecution,
  selectedNodeId,
  graphData,
  onToggleGraphFullscreen,
  onResumeNode,
  getResumeDisabledReasonForNode,
  savedGraphViewport,
  onGraphViewportChange,
  selectedNode,
  selectedResumeDisabledReason,
  resumeEventName
}: Readonly<ExecutionDashboardViewProps>) {
  const defaultHeaderNav = (
    <div className="flex items-center gap-3 text-xs">
      <a className="text-zinc-600 hover:underline" href="/dashboard">
        ダッシュボード
      </a>
      <a className="text-zinc-600 hover:underline" href="/playground">
        Playground
      </a>
      <a className="text-zinc-600 hover:underline" href="/health">
        health
      </a>
    </div>
  );

  const graphWrapperClassName = graphFullscreen ? "fixed inset-0 z-50 bg-zinc-50 p-4" : "";
  const graphMainClassName = graphFullscreen
    ? "mx-auto grid h-full max-w-[1600px] gap-4 lg:grid-cols-[minmax(0,1.8fr)_380px]"
    : "grid gap-4 lg:grid-cols-[1.6fr_1fr]";
  const graphSectionClassName = graphFullscreen ? "min-h-0" : "";
  const graphContainerClassName = `space-y-2 ${graphFullscreen ? "flex h-full min-h-0 flex-col" : ""}`;

  return (
    <div className={graphFullscreen ? "" : "space-y-4"}>
      {!graphFullscreen && (
        <>
          <header className="flex items-center justify-between">
            <h1 className="text-xl font-bold">{headerTitle}</h1>
            {headerNav ?? defaultHeaderNav}
          </header>

          <Toast toast={toast} onClose={onCloseToast} />

          <ExecutionHeader
            executionId={executionId}
            onExecutionIdChange={onExecutionIdChange}
            onLoad={onLoadExecution}
            onCancel={onCancelExecution}
            loading={loading}
            canCancel={canCancel}
            execution={execution}
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            compareMode={compareMode}
            onCompareModeChange={onCompareModeChange}
            streamEnabled={streamEnabled}
            onStreamEnabledChange={onStreamEnabledChange}
          />

          {compareMode && showExecutionPanels && (
            <ExecutionComparisonBar
              executionLeft={execution}
              executionRight={executionB}
              executionIdRight={executionIdB}
              onExecutionIdRightChange={onExecutionIdBChange}
              onLoadRight={onLoadExecutionB}
              loadingRight={loadingB}
              diff={executionDiff}
              onSelectDiffNode={onSelectNode}
            />
          )}

          <TenantMissingBanner />
          <ExecutionStatusBanner cancelRequested={!!execution?.cancelRequested} terminal={terminal} />

          {showExecutionPanels && isReplaying && (
            <ReplayBanner onBackToCurrent={onBackToCurrent} />
          )}

          {showExecutionPanels && (
            <ExecutionTimeline
              events={timelineEvents}
              loading={timelineLoading}
              error={timelineError}
              selectedSeq={replayAtSeq}
              onSelectSeq={onSelectSeq}
              onBackToCurrent={onBackToCurrent}
              isReplaying={isReplaying}
              hasMore={timelineHasMore}
              loadingMore={timelineLoadingMore}
              onLoadMore={onTimelineLoadMore}
            />
          )}
        </>
      )}

      {showExecutionPanels && (
        <div className={graphWrapperClassName}>
          <main className={graphMainClassName}>
            <section className={graphSectionClassName}>
              {viewMode === "list" ? (
                <NodeListView
                  nodes={displayExecution?.nodes ?? []}
                  selectedNodeId={selectedNodeId}
                  onSelectNode={onSelectNode}
                />
              ) : (
                <div className={graphContainerClassName}>
                  <div className="flex justify-end">
                    <button
                      className="rounded-xl border border-zinc-200 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 hover:bg-zinc-100"
                      onClick={onToggleGraphFullscreen}
                    >
                      {graphFullscreen ? "全画面終了 (Esc)" : "全画面表示"}
                    </button>
                  </div>
                  {graphData && !graphData.definitionBased && !graphFullscreen && (
                    <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                      graphId: {graphData.graphId} の定義が未登録のため、仮エッジ表示です。
                    </div>
                  )}
                  {graphData && (
                    <NodeGraphView
                      nodes={graphData.nodes}
                      edges={graphData.edges}
                      groups={graphData.groups}
                      selectedNodeId={selectedNodeId}
                      onSelectNode={onSelectNode}
                      onResumeNode={onResumeNode}
                      getResumeDisabledReason={getResumeDisabledReasonForNode}
                      defaultViewport={savedGraphViewport}
                      onViewportChange={onGraphViewportChange}
                      heightClassName={graphFullscreen ? "h-full min-h-[360px]" : undefined}
                      nodeDiffHighlight={compareMode ? executionDiff?.nodeHighlights : undefined}
                    />
                  )}
                </div>
              )}
            </section>

            <NodeDetail
              execution={displayExecution ?? execution}
              node={selectedNode}
              loading={loading}
              onResume={() => !isReplaying && selectedNodeId && onResumeNode(selectedNodeId)}
              resumeDisabledReason={selectedResumeDisabledReason}
              resumeEventName={resumeEventName}
              className={graphFullscreen ? "h-full min-h-0 overflow-auto" : undefined}
            />
          </main>
        </div>
      )}
    </div>
  );
}
