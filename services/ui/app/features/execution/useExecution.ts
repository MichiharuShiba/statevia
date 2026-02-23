"use client";

import { useEffect, useRef, useState } from "react";
import { apiGet, apiPost } from "../../lib/api";
import { applyExecutionStreamEvent, parseExecutionStreamEvent } from "../../lib/executionStream";
import type { CommandAccepted, ExecutionDTO } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["COMPLETED", "FAILED", "CANCELED"]);
const STREAM_RECONNECT_BASE_MS = 1000;
const STREAM_RECONNECT_MAX_MS = 30000;

function isTerminalExecution(status: ExecutionDTO["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

export type UseExecutionOptions = {
  onError?: (error: unknown) => void;
  onCancelSuccess?: () => void;
};

export function useExecution(executionId: string, options: UseExecutionOptions = {}) {
  const { onError, onCancelSuccess } = options;
  const [execution, setExecution] = useState<ExecutionDTO | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const onErrorRef = useRef(onError);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  const terminal = execution ? isTerminalExecution(execution.status) : false;
  const canCancel = !!execution && !terminal;

  const applyExecutionSnapshot = (response: ExecutionDTO) => {
    setExecution(response);
    setSelectedNodeId((current) => {
      if (current && response.nodes.some((node) => node.nodeId === current)) return current;
      return response.nodes[0]?.nodeId ?? null;
    });
  };

  const refreshExecutionSnapshot = async (id: string) => {
    const response = await apiGet<ExecutionDTO>(`/executions/${id}`);
    applyExecutionSnapshot(response);
  };

  useEffect(() => {
    if (!execution?.executionId) return;

    const currentExecutionId = execution.executionId;
    let disposed = false;
    let stream: EventSource | null = null;
    let reconnectAttempt = 0;
    let reconnectTimer: number | null = null;
    let hasConnectedOnce = false;

    const clearReconnectTimer = () => {
      if (reconnectTimer !== null) {
        window.clearTimeout(reconnectTimer);
        reconnectTimer = null;
      }
    };

    const scheduleReconnect = () => {
      if (disposed) return;
      clearReconnectTimer();
      const delay = Math.min(STREAM_RECONNECT_BASE_MS * 2 ** reconnectAttempt, STREAM_RECONNECT_MAX_MS);
      reconnectAttempt += 1;
      reconnectTimer = window.setTimeout(() => {
        if (!disposed) connectStream();
      }, delay);
    };

    const applyRawEvent = (raw: string) => {
      const parsed = parseExecutionStreamEvent(raw);
      if (!parsed) return;
      setExecution((current) => (current ? applyExecutionStreamEvent(current, parsed) : current));
    };

    const connectStream = () => {
      if (disposed) return;

      const next = new EventSource(`/api/core/executions/${encodeURIComponent(currentExecutionId)}/stream`);
      stream = next;

      next.onopen = () => {
        reconnectAttempt = 0;
        if (!hasConnectedOnce) {
          hasConnectedOnce = true;
          return;
        }
        void refreshExecutionSnapshot(currentExecutionId).catch(() => {
          // Keep stream alive; next events or reconnect attempts will converge state.
        });
      };

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
  }, [execution?.executionId]);

  async function loadExecution() {
    setLoading(true);
    try {
      const response = await apiGet<ExecutionDTO>(`/executions/${executionId}`);
      applyExecutionSnapshot(response);
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
      await apiPost<CommandAccepted>(`/executions/${execution.executionId}/cancel`, { reason: "ui" });
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
