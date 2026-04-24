"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { ExecutionComparisonBar } from "./ExecutionComparisonBar";
import { ExecutionHeader } from "./ExecutionHeader";
import { ExecutionStatusBanner } from "./ExecutionStatusBanner";
import { ExecutionTimeline } from "./ExecutionTimeline";
import { ReplayBanner } from "./ReplayBanner";
import { TenantMissingBanner } from "./TenantMissingBanner";
import { ActionLinkGroup } from "../layout/ActionLinkGroup";
import { PageState } from "../layout/PageState";
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
  /** false のとき executionId を URL 固定として編集させない。 */
  executionIdEditable?: boolean;
  /** false のとき比較モード UI を出さない。 */
  comparisonEnabled?: boolean;
  /** false のとき Cancel / Resume / Event などの実行操作を無効化する。 */
  operationsEnabled?: boolean;
  /** 初期の表示モード。 */
  initialViewMode?: ViewMode;
  /** true のとき View モード切り替えを固定し、UI から変更不可にする。 */
  lockViewMode?: boolean;
};

type ExecutionDashboardViewProps = {
  graphFullscreen: boolean;
  headerTitle: string;
  headerNav?: ReactNode;
  toast: ToastState | null;
  onCloseToast: () => void;
  executionId: string;
  executionIdEditable: boolean;
  onExecutionIdChange: (executionId: string) => void;
  onLoadExecution: () => void;
  onCancelExecution: () => void;
  loading: boolean;
  canCancel: boolean;
  onPublishEvent: (eventName: string) => void;
  execution: WorkflowView | null;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  showViewToggle: boolean;
  compareMode: boolean;
  onCompareModeChange: (compareMode: boolean) => void;
  comparisonEnabled: boolean;
  operationsEnabled: boolean;
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
  headerTitle = "実行の詳細",
  executionIdEditable = true,
  comparisonEnabled = true,
  operationsEnabled = true,
  initialViewMode = "list",
  lockViewMode = false
}: Readonly<ExecutionDashboardProps>) {
  const [executionId, setExecutionId] = useState(initialExecutionId);
  const [viewMode, setViewMode] = useState<ViewMode>(initialViewMode);
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
    if (lockViewMode) setViewMode(initialViewMode);
  }, [initialViewMode, lockViewMode]);

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
      onPublishSuccess: () => setToast({ tone: "success", message: "PublishEvent accepted" }),
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
    publishEvent,
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
    commandsEnabled: operationsEnabled,
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
    : getResumeDisabledReason(execution, selectedNode, operationsEnabled);

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
        : getResumeDisabledReason(execution, node, operationsEnabled);
    },
    [displayExecution, graphData, isReplaying, execution, operationsEnabled]
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
      executionIdEditable={executionIdEditable}
      onExecutionIdChange={setExecutionId}
      onLoadExecution={loadExecution}
      onCancelExecution={cancelExecution}
      loading={loading}
      canCancel={canCancel}
      onPublishEvent={publishEvent}
      execution={execution}
      viewMode={viewMode}
      onViewModeChange={(mode) => {
        if (lockViewMode) return;
        setViewMode(mode);
      }}
      showViewToggle={!lockViewMode}
      compareMode={compareMode}
      onCompareModeChange={setCompareMode}
      comparisonEnabled={comparisonEnabled}
      operationsEnabled={operationsEnabled}
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
  executionIdEditable,
  onExecutionIdChange,
  onLoadExecution,
  onCancelExecution,
  loading,
  canCancel,
  onPublishEvent,
  execution,
  viewMode,
  onViewModeChange,
  showViewToggle,
  compareMode,
  onCompareModeChange,
  comparisonEnabled,
  operationsEnabled,
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
  const [eventName, setEventName] = useState("");
  const defaultHeaderNav = (
    <ActionLinkGroup
      links={[
        { label: "ダッシュボード", href: "/dashboard", priority: "primary" },
        { label: "Workflow 一覧", href: "/workflows" },
        { label: "health", href: "/health" }
      ]}
    />
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
          <header className="flex flex-col items-start gap-3 rounded-2xl border border-[var(--tone-border)] bg-[var(--tone-surface-bg)] px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
            <h1 className="text-xl font-bold text-[var(--tone-fg-strong)]">{headerTitle}</h1>
            {headerNav ?? defaultHeaderNav}
          </header>

          <ExecutionHeader
            executionId={executionId}
            executionIdEditable={executionIdEditable}
            onExecutionIdChange={onExecutionIdChange}
            onLoad={onLoadExecution}
            onCancel={onCancelExecution}
            loading={loading}
            canCancel={canCancel}
            execution={execution}
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            showViewToggle={showViewToggle}
            compareMode={compareMode}
            onCompareModeChange={comparisonEnabled ? onCompareModeChange : undefined}
            streamEnabled={streamEnabled}
            onStreamEnabledChange={onStreamEnabledChange}
            showCancelAction={operationsEnabled}
          />

          <Toast toast={toast} onClose={onCloseToast} />

          {operationsEnabled && showExecutionPanels && (
            <section className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
              <h2 className="text-sm font-semibold text-zinc-800">実行操作</h2>
              <div className="mt-3 flex flex-wrap items-end gap-2">
                <label className="block min-w-[14rem] flex-1 text-xs text-zinc-600">
                  <span>Event 名（POST /events）</span>
                  <input
                    className="mt-1 w-full rounded-xl border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400"
                    value={eventName}
                    onChange={(event) => setEventName(event.target.value)}
                    placeholder="event-name"
                    autoComplete="off"
                  />
                </label>
                <button
                  type="button"
                  className="rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50 disabled:opacity-50"
                  disabled={loading || !eventName.trim() || !execution || terminal}
                  onClick={() => {
                    onPublishEvent(eventName.trim());
                    setEventName("");
                  }}
                >
                  Event 送信
                </button>
              </div>
              <p className="mt-2 text-xs text-zinc-500">
                Cancel / Resume / Event 送信は Run 画面に集約しています。
              </p>
            </section>
          )}

          {comparisonEnabled && compareMode && showExecutionPanels && (
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

          {!loading && !showExecutionPanels && (
            <PageState state="error" message="指定されたワークフローが見つかりませんでした。ID を確認してください。" />
          )}

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
              showResumeAction={operationsEnabled}
              className={graphFullscreen ? "h-full min-h-0 overflow-auto" : undefined}
            />
          </main>
        </div>
      )}
    </div>
  );
}
