"use client";

import { useEffect, useRef, useState, type Dispatch, type SetStateAction } from "react";
import { apiGet, apiPost } from "../../lib/api";
import { startExecutionStreamLifecycle } from "./executionStreamLifecycle";
import { isWithinMaxLength, matchesPattern } from "../../lib/validation/primitives";
import { EVENT_NAME_MAX_LENGTH, EVENT_NAME_PATTERN } from "../../lib/validation/formRules";
import { buildExecutionView } from "../../lib/executionView";
import type { CommandAccepted, ExecutionDTO, ExecutionGraphDTO, ExecutionView } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);
/** SSE 受信後に GET で Read Model を確定するまでの待ち（ms）。 */
export const DEFAULT_STREAM_REFRESH_DEBOUNCE_MS = 500;
/** SSE オフ時のポーリング間隔（ms）。 */
const POLL_INTERVAL_MS = 2500;

function isTerminalExecution(status: ExecutionView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

/** useExecution のコールバックオプション。 */
export type UseExecutionOptions = {
  onError?: (error: unknown) => void;
  onCancelSuccess?: () => void;
  onPublishSuccess?: () => void;
  /** false のとき EventSource を開かず、Running 中はポーリングで更新する。既定 true。 */
  streamEnabled?: boolean;
  /** SSE イベント後のフル GET に使うデバウンス（ms）。既定 `DEFAULT_STREAM_REFRESH_DEBOUNCE_MS`。 */
  streamRefreshDebounceMs?: number;
};

/** useExecution の戻り値。 */
export type UseExecutionResult = {
  execution: ExecutionView | null;
  loading: boolean;
  canCancel: boolean;
  terminal: boolean;
  loadExecution: () => Promise<void>;
  cancelExecution: () => Promise<void>;
  publishEvent: (eventName: string) => Promise<void>;
  selectedNodeId: string | null;
  setSelectedNodeId: Dispatch<SetStateAction<string | null>>;
};

/** 単一実行の読み込み・更新・SSE 再接続を管理するフック。 */
export function useExecution(executionDisplayId: string, options: UseExecutionOptions = {}): UseExecutionResult {
  const {
    onError,
    onCancelSuccess,
    onPublishSuccess,
    streamEnabled = true,
    streamRefreshDebounceMs = DEFAULT_STREAM_REFRESH_DEBOUNCE_MS
  } = options;
  const [execution, setExecution] = useState<ExecutionView | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const onErrorRef = useRef(onError);
  const activeStreamRef = useRef<EventSource | null>(null);

  useEffect(() => {
    onErrorRef.current = onError;
  }, [onError]);

  const terminal = execution ? isTerminalExecution(execution.status) : false;
  const canCancel = !!execution && !terminal;

  const applyExecutionSnapshot = (view: ExecutionView) => {
    setExecution(view);
    setSelectedNodeId((current) => {
      if (!current) return view.nodes[0]?.executionNodeId ?? null;
      const key = current.trim();
      if (view.nodes.some((node) => node.executionNodeId === key)) return current;
      if (view.nodes.some((node) => (node.stateName?.trim() ?? "") === key)) return current;
      return view.nodes[0]?.executionNodeId ?? null;
    });
  };

  const refreshExecutionSnapshot = async (displayId: string) => {
    const executionDto = await apiGet<ExecutionDTO>(`/executions/${displayId}`);
    let graph: ExecutionGraphDTO | null = null;
    try {
      graph = await apiGet<ExecutionGraphDTO>(`/executions/${displayId}/graph`);
    } catch {
      // graph 未取得時は nodes 空で表示
    }
    const view = buildExecutionView(executionDto, graph);
    applyExecutionSnapshot(view);
  };

  const refreshSnapshotRef = useRef(refreshExecutionSnapshot);
  refreshSnapshotRef.current = refreshExecutionSnapshot;

  useEffect(() => {
    if (!streamEnabled) return;
    if (!execution?.displayId) return;
    if (terminal) return;

    return startExecutionStreamLifecycle({
      displayId: execution.displayId,
      streamRefreshDebounceMs,
      refreshSnapshot: (displayId) => refreshSnapshotRef.current(displayId),
      setExecution,
      activeStreamRef
    });
  }, [execution?.displayId, streamEnabled, streamRefreshDebounceMs, terminal]);

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
      await refreshExecutionSnapshot(executionDisplayId);
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
      await apiPost<CommandAccepted>(`/executions/${displayId}/cancel`, { reason: "ui" });
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
    if (!isWithinMaxLength(name, EVENT_NAME_MAX_LENGTH) || !matchesPattern(name, EVENT_NAME_PATTERN)) return;
    setLoading(true);
    let posted = false;
    try {
      await apiPost<CommandAccepted>(`/executions/${displayId}/events`, { name });
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
