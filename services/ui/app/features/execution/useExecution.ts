"use client";

import { useEffect, useRef, useState } from "react";
import { apiGet, apiPost, getApiConfig } from "../../lib/api";
import { applyExecutionStreamEvent, parseExecutionStreamEvent } from "../../lib/executionStream";
import { buildWorkflowView } from "../../lib/workflowView";
import type { CommandAccepted, WorkflowDTO, WorkflowGraphDTO, WorkflowView } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);
const STREAM_RECONNECT_BASE_MS = 1000;
const STREAM_RECONNECT_MAX_MS = 30000;

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
};

export function useExecution(workflowDisplayId: string, options: UseExecutionOptions = {}) {
  const { onError, onCancelSuccess } = options;
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
    const workflow = await apiGet<WorkflowDTO>(`/executions/${displayId}`);
    let graph: WorkflowGraphDTO | null = null;
    try {
      graph = await apiGet<WorkflowGraphDTO>(`/executions/${displayId}/graph`);
    } catch {
      // graph 未取得時は nodes 空で表示
    }
    const view = buildWorkflowView(workflow, graph);
    applyExecutionSnapshot(view);
  };

  useEffect(() => {
    if (!execution?.displayId) return;

    const currentDisplayId = execution.displayId;
    let disposed = false;
    let stream: EventSource | null = null;
    let reconnectAttempt = 0;
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
    let hasConnectedOnce = false;

    const clearReconnectTimer = () => {
      if (reconnectTimer !== null) {
        globalThis.clearTimeout(reconnectTimer);
        reconnectTimer = null;
      }
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
    };

    const noop = () => {};
    const onStreamOpen = () => {
      reconnectAttempt = 0;
      if (!hasConnectedOnce) {
        hasConnectedOnce = true;
        return;
      }
      void refreshExecutionSnapshot(currentDisplayId).catch(noop);
    };

    const connectStream = () => {
      if (disposed) return;

      const { tenantId } = getApiConfig();
      const streamPath = `/api/core/executions/${encodeURIComponent(currentDisplayId)}/stream`;
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
      stream?.close();
    };
  }, [execution?.displayId]);

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
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(`/executions/${execution.displayId}/cancel`, { reason: "ui" });
      onCancelSuccess?.();
      await loadExecution();
    } catch (error) {
      onErrorRef.current?.(error);
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
    selectedNodeId,
    setSelectedNodeId
  };
}
