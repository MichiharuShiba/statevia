"use client";

import { useEffect, useMemo, useState } from "react";
import { ExecutionHeader } from "./components/ExecutionHeader";
import { NodeDetail } from "./components/NodeDetail";
import { NodeGraphView } from "./components/NodeGraphView";
import { NodeListView } from "./components/NodeListView";
import { Toast, type ToastState } from "./components/Toast";
import type { ViewMode } from "./components/ViewToggle";
import { getGraphDefinition } from "./graphs/registry";
import { apiGet, apiPost } from "./lib/api";
import { resolveGroupBounds } from "./lib/grouping";
import { layoutGraph } from "./lib/graphLayout";
import { mergeGraph } from "./lib/mergeGraph";
import type { ApiError, CommandAccepted, ExecutionDTO, ExecutionNodeDTO } from "./lib/types";

const TERMINAL_STATUSES = new Set(["COMPLETED", "FAILED", "CANCELED"]);

function isTerminalExecution(status: ExecutionDTO["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

function toToastError(error: unknown): ToastState {
  const apiError = error as ApiError;
  const status = apiError?.status;
  const code = apiError?.error?.code ?? "UNKNOWN";
  const message = apiError?.error?.message ?? "Unknown error";

  if (status === 409) {
    return { tone: "error", message: `409 状態競合: ${code} - ${message}` };
  }
  if (status === 422) {
    return { tone: "error", message: `422 入力不正: ${code} - ${message}` };
  }
  if (status === 500) {
    return { tone: "error", message: `500 サーバーエラー: ${code} - ${message}` };
  }
  return { tone: "error", message: `${code}: ${message}` };
}

function getResumeDisabledReason(execution: ExecutionDTO | null, node: ExecutionNodeDTO | null): string | null {
  if (!execution) return "Execution が未読込です";
  if (!node) return "Node を選択してください";
  if (isTerminalExecution(execution.status)) return "Executionは終了しています";
  if (execution.cancelRequestedAt) return "Cancel要求済みのため、Resumeなど進行系操作はできません";
  if (node.status !== "WAITING") return "WAITING 状態のノードのみ Resume できます";
  return null;
}

export default function Page() {
  const [executionId, setExecutionId] = useState("ex-1");
  const [execution, setExecution] = useState<ExecutionDTO | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [graphFullscreen, setGraphFullscreen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  const terminal = execution ? isTerminalExecution(execution.status) : false;
  const canCancel = !!execution && !terminal;

  const graphDefinition = execution ? getGraphDefinition(execution.graphId) : null;

  const graphData = useMemo(() => {
    if (!execution) return null;
    const merged = mergeGraph(execution, graphDefinition);
    const positioned = layoutGraph(merged.nodes, merged.edges.map((edge) => ({ ...edge })), merged.layoutHints);
    const groups = resolveGroupBounds(positioned.nodes, positioned.edges, merged.groups, merged.layoutHints);
    return {
      graphId: execution.graphId,
      definitionBased: merged.isDefinitionBased,
      mergedNodes: merged.nodes,
      nodes: positioned.nodes,
      edges: positioned.edges,
      groups
    };
  }, [execution, graphDefinition]);

  const getNodeWithFallback = (nodeId: string): ExecutionNodeDTO | null => {
    if (!execution) return null;
    const runtimeNode = execution.nodes.find((node) => node.nodeId === nodeId);
    if (runtimeNode) return runtimeNode;

    const mergedNode = graphData?.mergedNodes.find((node) => node.nodeId === nodeId);
    if (!mergedNode) return null;
    return {
      nodeId: mergedNode.nodeId,
      nodeType: mergedNode.nodeType,
      status: mergedNode.status,
      attempt: mergedNode.attempt,
      workerId: null,
      waitKey: mergedNode.waitKey,
      canceledByExecution: mergedNode.canceledByExecution
    };
  };

  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null;
    return getNodeWithFallback(selectedNodeId);
  }, [selectedNodeId, execution, graphData]);

  useEffect(() => {
    if (viewMode !== "graph" || !execution) {
      setGraphFullscreen(false);
    }
  }, [viewMode, execution]);

  useEffect(() => {
    if (!graphFullscreen) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setGraphFullscreen(false);
      }
    };

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = originalOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [graphFullscreen]);

  async function loadExecution() {
    setLoading(true);
    try {
      const response = await apiGet<ExecutionDTO>(`/executions/${executionId}`);
      setExecution(response);
      if (!selectedNodeId || !response.nodes.some((n) => n.nodeId === selectedNodeId)) {
        setSelectedNodeId(response.nodes[0]?.nodeId ?? null);
      }
    } catch (error) {
      setExecution(null);
      setSelectedNodeId(null);
      setToast(toToastError(error));
    } finally {
      setLoading(false);
    }
  }

  async function cancelExecution() {
    if (!execution) return;
    setLoading(true);
    try {
      const response = await apiPost<CommandAccepted>(`/executions/${execution.executionId}/cancel`, { reason: "ui" });
      setToast({ tone: "success", message: `${response.command} accepted` });
      await loadExecution();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setLoading(false);
    }
  }

  async function resumeSelectedNode(nodeId?: string) {
    if (!execution) return;
    const node = execution.nodes.find((item) => item.nodeId === (nodeId ?? selectedNode?.nodeId));
    if (!node) return;
    setLoading(true);
    try {
      const response = await apiPost<CommandAccepted>(
        `/executions/${execution.executionId}/nodes/${node.nodeId}/resume`,
        { resumeKey: node.waitKey ?? undefined }
      );
      setToast({ tone: "success", message: `${response.command} accepted` });
      await loadExecution();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setLoading(false);
    }
  }

  const selectedResumeDisabledReason = getResumeDisabledReason(execution, selectedNode);
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

          {execution?.cancelRequestedAt && (
            <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-900">
              Cancel要求済みのため、Resumeなど進行系操作はできません
            </div>
          )}

          {terminal && (
            <div className="rounded-xl border border-zinc-300 bg-zinc-100 px-3 py-2 text-xs text-zinc-800">
              Executionは終了しています
            </div>
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
                  nodes={execution.nodes}
                  selectedNodeId={selectedNodeId}
                  onSelectNode={(nodeId) => setSelectedNodeId(nodeId)}
                />
              ) : (
                <div className={`space-y-2 ${graphFullscreen ? "flex h-full min-h-0 flex-col" : ""}`}>
                  <div className="flex justify-end">
                    <button
                      className="rounded-xl border border-zinc-200 bg-white px-3 py-1.5 text-xs font-semibold text-zinc-700 hover:bg-zinc-100"
                      onClick={() => setGraphFullscreen((current) => !current)}
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
                      onResumeNode={(nodeId) => resumeSelectedNode(nodeId)}
                      getResumeDisabledReason={(nodeId) => {
                        const node = getNodeWithFallback(nodeId);
                        return getResumeDisabledReason(execution, node);
                      }}
                      heightClassName={graphFullscreen ? "h-full min-h-[360px]" : undefined}
                    />
                  )}
                </div>
              )}
            </section>

            <NodeDetail
              execution={execution}
              node={selectedNode}
              loading={loading}
              onResume={() => resumeSelectedNode()}
              resumeDisabledReason={selectedResumeDisabledReason}
              className={graphFullscreen ? "h-full min-h-0 overflow-auto" : undefined}
            />
          </main>
        </div>
      )}
    </div>
  );
}
