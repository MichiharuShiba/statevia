"use client";

import { useEffect, useRef, useState } from "react";
import { apiGet, apiPost, getApiConfig } from "../../lib/api";
import { applyExecutionStreamEvent, parseExecutionStreamEvent } from "../../lib/executionStream";
import { buildWorkflowView } from "../../lib/workflowView";
import type { CommandAccepted, WorkflowDTO, WorkflowGraphDTO, WorkflowView } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);
const STREAM_RECONNECT_BASE_MS = 1000;
const STREAM_RECONNECT_MAX_MS = 30000;
/** SSE 受信後に GET で Read Model を確定するまでの待ち（ms）。 */
export const DEFAULT_STREAM_REFRESH_DEBOUNCE_MS = 500;
/** SSE オフ時のポーリング間隔（ms）。 */
const POLL_INTERVAL_MS = 2500;

/** 指数バックオフの遅延（ms）を計算する。テスト・再利用用に export。 */
export function getReconnectDelayMs(attempt: number, baseMs: number, maxMs: number): number {
  return Math.min(baseMs * 2 ** attempt, maxMs);
}

function isTerminalExecution(status: WorkflowView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

export type UseExecutionOptions = {
  onError?: (error: unknown) => void;
  onCancelSuccess?: () => void;
  onPublishSuccess?: () => void;
  /** false のとき EventSource を開かず、Running 中はポーリングで更新する。既定 true。 */
  streamEnabled?: boolean;
  /** SSE イベント後のフル GET に使うデバウンス（ms）。既定 `DEFAULT_STREAM_REFRESH_DEBOUNCE_MS`。 */
  streamRefreshDebounceMs?: number;
};

export function useExecution(workflowDisplayId: string, options: UseExecutionOptions = {}) {
  const {
    onError,
    onCancelSuccess,
    onPublishSuccess,
    streamEnabled = true,
    streamRefreshDebounceMs = DEFAULT_STREAM_REFRESH_DEBOUNCE_MS
  } = options;
  const [execution, setExecution] = useState<WorkflowView | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const onErrorRef = useRef(onError);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  const terminal = execution ? isTerminalExecution(execution.status) : false;
  const canCancel = !!execution && !terminal;

  const applyExecutionSnapshot = (view: WorkflowView) => {
    setExecution(view);
    setSelectedNodeId((current) => {
      if (current && view.nodes.some((node) => node.nodeId === current)) return current;
      return view.nodes[0]?.nodeId ?? null;
    });
  };

  const refreshExecutionSnapshot = async (displayId: string) => {
    const workflow = await apiGet<WorkflowDTO>(`/workflows/${displayId}`);
    let graph: WorkflowGraphDTO | null = null;
    try {
      graph = await apiGet<WorkflowGraphDTO>(`/workflows/${displayId}/graph`);
    } catch {
      // graph 未取得時は nodes 空で表示
    }
    const view = buildWorkflowView(workflow, graph);
    applyExecutionSnapshot(view);
  };

  const refreshSnapshotRef = useRef(refreshExecutionSnapshot);
  refreshSnapshotRef.current = refreshExecutionSnapshot;

  useEffect(() => {
    if (!streamEnabled) return;
    if (!execution?.displayId) return;

    const currentDisplayId = execution.displayId;
    let disposed = false;
    let stream: EventSource | null = null;
    let reconnectAttempt = 0;
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
    let getDebounceTimer: ReturnType<typeof setTimeout> | null = null;
    let hasConnectedOnce = false;

    const clearReconnectTimer = () => {
      if (reconnectTimer !== null) {
        globalThis.clearTimeout(reconnectTimer);
        reconnectTimer = null;
      }
    };

    const clearGetDebounce = () => {
      if (getDebounceTimer !== null) {
        globalThis.clearTimeout(getDebounceTimer);
        getDebounceTimer = null;
      }
    };

    const scheduleDebouncedGet = () => {
      clearGetDebounce();
      getDebounceTimer = globalThis.setTimeout(() => {
        getDebounceTimer = null;
        if (!disposed) void refreshSnapshotRef.current(currentDisplayId).catch(() => {});
      }, streamRefreshDebounceMs);
    };

    const scheduleReconnect = () => {
      if (disposed) return;
      clearReconnectTimer();
      const delay = getReconnectDelayMs(reconnectAttempt, STREAM_RECONNECT_BASE_MS, STREAM_RECONNECT_MAX_MS);
      reconnectAttempt += 1;
      reconnectTimer = globalThis.setTimeout(() => {
        if (!disposed) connectStream();
      }, delay);
    };

    const applyRawEvent = (raw: string) => {
      const parsed = parseExecutionStreamEvent(raw);
      if (!parsed) return;
      setExecution((current) => (current ? applyExecutionStreamEvent(current, parsed) : current));
      scheduleDebouncedGet();
    };

    const noop = () => {};
    const onStreamOpen = () => {
      reconnectAttempt = 0;
      if (!hasConnectedOnce) {
        hasConnectedOnce = true;
        return;
      }
      void refreshSnapshotRef.current(currentDisplayId).catch(noop);
    };

    const connectStream = () => {
      if (disposed) return;

      const { tenantId } = getApiConfig();
      const streamPath = `/api/core/workflows/${encodeURIComponent(currentDisplayId)}/stream`;
      const streamUrl = tenantId
        ? `${streamPath}?${new URLSearchParams({ tenantId }).toString()}`
        : streamPath;
      const next = new EventSource(streamUrl);
      stream = next;

      next.onopen = onStreamOpen;

      next.onmessage = (event: MessageEvent<string>) => applyRawEvent(event.data);
      next.addEventListener("GraphUpdated", (e) => applyRawEvent((e as MessageEvent<string>).data));
      next.addEventListener("ExecutionStatusChanged", (e) => applyRawEvent((e as MessageEvent<string>).data));
      next.addEventListener("NodeCancelled", (e) => applyRawEvent((e as MessageEvent<string>).data));
      next.addEventListener("NodeFailed", (e) => applyRawEvent((e as MessageEvent<string>).data));

      next.onerror = () => {
        if (disposed) return;
        next.close();
        if (stream === next) {
          stream = null;
        }
        scheduleReconnect();
      };
    };

    connectStream();

    return () => {
      disposed = true;
      clearReconnectTimer();
      clearGetDebounce();
      stream?.close();
    };
  }, [execution?.displayId, streamEnabled, streamRefreshDebounceMs]);

  useEffect(() => {
    if (streamEnabled) return;
    if (!execution?.displayId) return;
    if (terminal) return;
    const displayId = execution.displayId;
    const tick = () => {
      void refreshSnapshotRef.current(displayId).catch(() => {});
    };
    tick();
    const interval = globalThis.setInterval(tick, POLL_INTERVAL_MS);
    return () => globalThis.clearInterval(interval);
  }, [streamEnabled, execution?.displayId, terminal]);

  async function loadExecution() {
    setLoading(true);
    try {
      await refreshExecutionSnapshot(workflowDisplayId);
    } catch (error) {
      setExecution(null);
      setSelectedNodeId(null);
      onErrorRef.current?.(error);
    } finally {
      setLoading(false);
    }
  }

  async function cancelExecution() {
    if (!execution) return;
    const displayId = execution.displayId;
    setLoading(true);
    let cancelPosted = false;
    try {
      await apiPost<CommandAccepted>(`/workflows/${displayId}/cancel`, { reason: "ui" });
      cancelPosted = true;
      // loadExecution は失敗を握りつぶすため、キャンセル後の再取得はここで判別する。
      await refreshExecutionSnapshot(displayId);
      onCancelSuccess?.();
    } catch (error) {
      onErrorRef.current?.(error);
      if (cancelPosted) {
        setExecution(null);
        setSelectedNodeId(null);
      }
    } finally {
      setLoading(false);
    }
  }

  async function publishEvent(eventName: string) {
    if (!execution) return;
    const displayId = execution.displayId;
    const name = eventName.trim();
    if (!name) return;
    setLoading(true);
    let posted = false;
    try {
      await apiPost<CommandAccepted>(`/workflows/${displayId}/events`, { name });
      posted = true;
      await refreshExecutionSnapshot(displayId);
      onPublishSuccess?.();
    } catch (error) {
      onErrorRef.current?.(error);
      if (posted) {
        setExecution(null);
        setSelectedNodeId(null);
      }
    } finally {
      setLoading(false);
    }
  }

  return {
    execution,
    loading,
    canCancel,
    terminal,
    loadExecution,
    cancelExecution,
    publishEvent,
    selectedNodeId,
    setSelectedNodeId
  };
}
