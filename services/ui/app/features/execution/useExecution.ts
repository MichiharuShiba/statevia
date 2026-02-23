"use client";

import { useEffect, useState } from "react";
import { apiGet, apiPost } from "../../lib/api";
import { applyExecutionStreamEvent, parseExecutionStreamEvent } from "../../lib/executionStream";
import type { CommandAccepted, ExecutionDTO } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["COMPLETED", "FAILED", "CANCELED"]);

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

  const terminal = execution ? isTerminalExecution(execution.status) : false;
  const canCancel = !!execution && !terminal;

  useEffect(() => {
    if (!execution?.executionId) return;

    const stream = new EventSource(`/api/core/executions/${encodeURIComponent(execution.executionId)}/stream`);
    const applyRawEvent = (raw: string) => {
      const parsed = parseExecutionStreamEvent(raw);
      if (!parsed) return;
      setExecution((current) => (current ? applyExecutionStreamEvent(current, parsed) : current));
    };

    stream.onmessage = (event: MessageEvent<string>) => applyRawEvent(event.data);
    stream.addEventListener("GraphUpdated", (e) => applyRawEvent((e as MessageEvent<string>).data));
    stream.addEventListener("ExecutionStatusChanged", (e) => applyRawEvent((e as MessageEvent<string>).data));
    stream.addEventListener("NodeCancelled", (e) => applyRawEvent((e as MessageEvent<string>).data));
    stream.addEventListener("NodeFailed", (e) => applyRawEvent((e as MessageEvent<string>).data));

    return () => stream.close();
  }, [execution?.executionId]);

  async function loadExecution() {
    setLoading(true);
    try {
      const response = await apiGet<ExecutionDTO>(`/executions/${executionId}`);
      setExecution(response);
      setSelectedNodeId((current) => {
        if (current && response.nodes.some((n) => n.nodeId === current)) return current;
        return response.nodes[0]?.nodeId ?? null;
      });
    } catch (error) {
      setExecution(null);
      setSelectedNodeId(null);
      onError?.(error);
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
      onError?.(error);
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
