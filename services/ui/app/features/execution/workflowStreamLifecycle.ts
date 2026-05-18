import type { Dispatch, MutableRefObject, SetStateAction } from "react";
import { getApiConfig } from "../../lib/api";
import { applyExecutionStreamEvent, parseExecutionStreamEvent } from "../../lib/executionStream";
import type { WorkflowView } from "../../lib/types";

const STREAM_RECONNECT_BASE_MS = 1000;
const STREAM_RECONNECT_MAX_MS = 30000;

/** 指数バックオフの遅延（ms）を計算する。テスト・再利用用に export。 */
export function getReconnectDelayMs(attempt: number, baseMs: number, maxMs: number): number {
  return Math.min(baseMs * 2 ** attempt, maxMs);
}

/** SSE 接続のライフサイクル制御に渡す依存。 */
export type WorkflowStreamLifecycleOptions = {
  displayId: string;
  streamRefreshDebounceMs: number;
  refreshSnapshot: (displayId: string) => Promise<void>;
  setExecution: Dispatch<SetStateAction<WorkflowView | null>>;
  activeStreamRef: MutableRefObject<EventSource | null>;
};

function buildWorkflowStreamUrl(displayId: string): string {
  const { tenantId } = getApiConfig();
  const streamPath = `/api/core/workflows/${encodeURIComponent(displayId)}/stream`;
  return tenantId ? `${streamPath}?${new URLSearchParams({ tenantId }).toString()}` : streamPath;
}

function bindStreamEventHandlers(
  stream: EventSource,
  applyRawEvent: (raw: string) => void,
  onStreamOpen: () => void,
  onStreamError: () => void
): void {
  stream.onopen = onStreamOpen;
  stream.onmessage = (event: MessageEvent<string>) => applyRawEvent(event.data);
  stream.addEventListener("GraphUpdated", (e) => applyRawEvent((e as MessageEvent<string>).data));
  stream.addEventListener("ExecutionStatusChanged", (e) => applyRawEvent((e as MessageEvent<string>).data));
  stream.addEventListener("NodeCancelled", (e) => applyRawEvent((e as MessageEvent<string>).data));
  stream.addEventListener("NodeFailed", (e) => applyRawEvent((e as MessageEvent<string>).data));
  stream.onerror = onStreamError;
}

/**
 * ワークフロー実行の SSE 接続を開始し、クリーンアップ関数を返す。
 * useExecution の useEffect から呼び出し、ネスト深度を抑える。
 */
export function startWorkflowStreamLifecycle(options: WorkflowStreamLifecycleOptions): () => void {
  const { displayId, streamRefreshDebounceMs, refreshSnapshot, setExecution, activeStreamRef } = options;

  let disposed = false;
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

  const runDebouncedRefresh = () => {
    getDebounceTimer = null;
    if (!disposed) void refreshSnapshot(displayId).catch(() => {});
  };

  const scheduleDebouncedGet = () => {
    clearGetDebounce();
    getDebounceTimer = globalThis.setTimeout(runDebouncedRefresh, streamRefreshDebounceMs);
  };

  const scheduleReconnect = () => {
    if (disposed) return;
    clearReconnectTimer();
    const delay = getReconnectDelayMs(reconnectAttempt, STREAM_RECONNECT_BASE_MS, STREAM_RECONNECT_MAX_MS);
    reconnectAttempt += 1;
    reconnectTimer = globalThis.setTimeout(connectStream, delay);
  };

  const applyRawEvent = (raw: string) => {
    const parsed = parseExecutionStreamEvent(raw);
    if (!parsed) return;
    setExecution((current) => (current ? applyExecutionStreamEvent(current, parsed) : current));
    scheduleDebouncedGet();
  };

  const onStreamOpen = () => {
    reconnectAttempt = 0;
    if (!hasConnectedOnce) {
      hasConnectedOnce = true;
      return;
    }
    void refreshSnapshot(displayId).catch(() => {});
  };

  const onStreamError = () => {
    if (disposed) return;
    const stream = activeStreamRef.current;
    stream?.close();
    if (activeStreamRef.current === stream) {
      activeStreamRef.current = null;
    }
    scheduleReconnect();
  };

  function connectStream() {
    if (disposed) return;
    activeStreamRef.current?.close();
    activeStreamRef.current = null;

    const next = new EventSource(buildWorkflowStreamUrl(displayId));
    activeStreamRef.current = next;
    bindStreamEventHandlers(next, applyRawEvent, onStreamOpen, onStreamError);
  }

  connectStream();

  return () => {
    disposed = true;
    clearReconnectTimer();
    clearGetDebounce();
    if (activeStreamRef.current) {
      activeStreamRef.current.close();
      activeStreamRef.current = null;
    }
  };
}
