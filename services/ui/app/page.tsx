"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ExecutionHeader } from "./components/execution/ExecutionHeader";
import { ExecutionStatusBanner } from "./components/execution/ExecutionStatusBanner";
import { ExecutionTimeline } from "./components/execution/ExecutionTimeline";
import { TenantMissingBanner } from "./components/execution/TenantMissingBanner";
import { NodeDetail } from "./components/nodes/NodeDetail";
import { NodeGraphView, type GraphViewport } from "./components/nodes/NodeGraphView";
import { NodeListView } from "./components/nodes/NodeListView";
import { Toast } from "./components/Toast";
import type { ViewMode } from "./components/ViewToggle";
import { getGraphDefinition } from "./graphs/registry";
import { useExecution } from "./features/execution/useExecution";
import { useExecutionEvents } from "./features/execution/useExecutionEvents";
import { useExecutionStateAtSeq } from "./features/execution/useExecutionStateAtSeq";
import { getNodeWithFallback, useGraphData } from "./features/graph/useGraphData";
import { getResumeDisabledReason, useNodeCommands } from "./features/nodes/useNodeCommands";
import { toToastError, type ToastState } from "./lib/errors";

/** executionId ごとの Graph ビューポート（ズーム・パン位置） */
type GraphViewportByExecutionId = Record<string, GraphViewport>;

/**
 * Graph 状態の復元ルール（#13）:
 * - ズーム・パン: executionId ごとに保持。List⇔Graph 切替時は保存した defaultViewport で復元。未保存時は fitView。
 * - 選択ノード: useExecution 内で保持。View 切替では変更しない。再ロード時のみ snapshot に応じて維持 or 先頭へ。
 */

export default function Page() {
  const [executionId, setExecutionId] = useState("ex-1");
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [graphFullscreen, setGraphFullscreen] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [graphViewportByExecutionId, setGraphViewportByExecutionId] = useState<GraphViewportByExecutionId>({});
  const [replayAtSeq, setReplayAtSeq] = useState<number | null>(null);

  const {
    execution,
    loading: executionLoading,
    canCancel,
    terminal,
    loadExecution,
    cancelExecution,
    selectedNodeId,
    setSelectedNodeId
  } = useExecution(executionId, {
    onError: (err) => setToast(toToastError(err)),
    onCancelSuccess: () => setToast({ tone: "success", message: "CancelExecution accepted" })
  });

  const { events: timelineEvents, loading: timelineLoading, error: timelineError } = useExecutionEvents(
    execution?.executionId ?? null
  );
  const { state: stateAtSeq, loading: stateAtSeqLoading } = useExecutionStateAtSeq(
    execution?.executionId ?? null,
    replayAtSeq
  );

  const displayExecution =
    replayAtSeq != null && stateAtSeq != null ? stateAtSeq : execution;
  const isReplaying = replayAtSeq != null && stateAtSeq != null;

  const graphDefinition = execution ? getGraphDefinition(execution.graphId) : null;
  const graphData = useGraphData(displayExecution, graphDefinition);

  const { resumeNode, loading: nodeLoading } = useNodeCommands(execution, {
    onSuccess: () => {
      setToast({ tone: "success", message: "ResumeNode accepted" });
      loadExecution();
    },
    onError: (err) => setToast(toToastError(err))
  });

  const loading = executionLoading || nodeLoading || stateAtSeqLoading;

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

  const savedGraphViewport = execution ? graphViewportByExecutionId[execution.executionId] : undefined;
  const handleGraphViewportChange = useCallback(
    (viewport: GraphViewport) => {
      if (displayExecution) {
        setGraphViewportByExecutionId((prev) => ({
          ...prev,
          [displayExecution.executionId]: viewport
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

  return (
    <div className={graphFullscreen ? "" : "space-y-4"}>
      {!graphFullscreen && (
        <>
          <header className="flex items-center justify-between">
            <h1 className="text-xl font-bold">Execution UI</h1>
            <a className="text-xs text-zinc-600 hover:underline" href="/health">
              health
            </a>
          </header>

          <Toast toast={toast} onClose={() => setToast(null)} />

          <ExecutionHeader
            executionId={executionId}
            onExecutionIdChange={setExecutionId}
            onLoad={loadExecution}
            onCancel={cancelExecution}
            loading={loading}
            canCancel={canCancel}
            execution={execution}
            viewMode={viewMode}
            onViewModeChange={setViewMode}
          />

          <TenantMissingBanner />
          <ExecutionStatusBanner cancelRequested={!!execution?.cancelRequestedAt} terminal={terminal} />

          {showExecutionPanels && (
            <ExecutionTimeline
              events={timelineEvents}
              loading={timelineLoading}
              error={timelineError}
              selectedSeq={replayAtSeq}
              onSelectSeq={setReplayAtSeq}
              onBackToCurrent={() => setReplayAtSeq(null)}
              isReplaying={isReplaying}
            />
          )}
        </>
      )}

      {showExecutionPanels && (
        <div className={graphFullscreen ? "fixed inset-0 z-50 bg-zinc-50 p-4" : ""}>
          <main
            className={
              graphFullscreen
                ? "mx-auto grid h-full max-w-[1600px] gap-4 lg:grid-cols-[minmax(0,1.8fr)_380px]"
                : "grid gap-4 lg:grid-cols-[1.6fr_1fr]"
            }
          >
            <section className={graphFullscreen ? "min-h-0" : ""}>
              {viewMode === "list" ? (
                <NodeListView
                  nodes={displayExecution?.nodes ?? []}
                  selectedNodeId={selectedNodeId}
                  onSelectNode={(id) => setSelectedNodeId(id)}
                />
              ) : (
                <div className={`space-y-2 ${graphFullscreen ? "flex h-full min-h-0 flex-col" : ""}`}>
                  <div className="flex justify-end">
                    <button
                      className="rounded-xl border border-zinc-200 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 hover:bg-zinc-100"
                      onClick={() => setGraphFullscreen((v) => !v)}
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
                      onSelectNode={setSelectedNodeId}
                      onResumeNode={(nodeId) => resumeNode(nodeId)}
                      getResumeDisabledReason={(nodeId) => {
                        const node = getNodeWithFallback(displayExecution, graphData, nodeId);
                        return isReplaying
                          ? "リプレイ表示中は実行できません"
                          : getResumeDisabledReason(execution, node);
                      }}
                      defaultViewport={savedGraphViewport}
                      onViewportChange={handleGraphViewportChange}
                      heightClassName={graphFullscreen ? "h-full min-h-[360px]" : undefined}
                    />
                  )}
                </div>
              )}
            </section>

            <NodeDetail
              execution={displayExecution ?? execution}
              node={selectedNode}
              loading={loading}
              onResume={() => !isReplaying && selectedNodeId && resumeNode(selectedNodeId)}
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
