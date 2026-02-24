import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useExecution, getReconnectDelayMs } from "../../../app/features/execution/useExecution";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../../app/lib/types";
import * as api from "../../../app/lib/api";

function execution(nodes: ExecutionNodeDTO[], overrides: Partial<ExecutionDTO> = {}): ExecutionDTO {
  return {
    executionId: "ex-1",
    status: "ACTIVE",
    graphId: "g-1",
    cancelRequestedAt: null,
    canceledAt: null,
    failedAt: null,
    completedAt: null,
    nodes,
    ...overrides
  };
}

describe("getReconnectDelayMs", () => {
  it("attempt 0 のとき baseMs を返す", () => {
    expect(getReconnectDelayMs(0, 1000, 30000)).toBe(1000);
  });

  it("attempt が増えると指数で増え、maxMs でキャップする", () => {
    expect(getReconnectDelayMs(1, 1000, 30000)).toBe(2000);
    expect(getReconnectDelayMs(2, 1000, 30000)).toBe(4000);
    expect(getReconnectDelayMs(5, 1000, 30000)).toBe(30000);
    expect(getReconnectDelayMs(10, 1000, 30000)).toBe(30000);
  });
});

/** jsdom に EventSource がないため、全 useExecution テストでスタブする最小クラス */
class MinimalEventSource {
  close = vi.fn();
  addEventListener = vi.fn();
  constructor(public readonly url: string) {}
}

describe("useExecution", () => {
  const defaultExecution = execution([
    { nodeId: "n-1", nodeType: "TASK", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false }
  ]);

  beforeEach(() => {
    vi.stubGlobal("EventSource", MinimalEventSource);
    vi.spyOn(api, "apiGet").mockResolvedValue(defaultExecution);
    vi.spyOn(api, "apiPost").mockResolvedValue({
      executionId: "ex-1",
      command: "cancel",
      accepted: true,
      idempotencyKey: "key-1"
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("初期状態で execution は null、loadExecution で取得できる", async () => {
    const { result } = renderHook(() => useExecution("ex-1"));

    expect(result.current.execution).toBeNull();

    await act(async () => {
      result.current.loadExecution();
    });

    expect(api.apiGet).toHaveBeenCalledWith("/executions/ex-1");
    expect(result.current.execution).not.toBeNull();
    expect(result.current.execution?.executionId).toBe("ex-1");
    expect(result.current.selectedNodeId).toBe("n-1");
  });

  it("loadExecution 失敗時に onError を呼び、execution を null にする", async () => {
    vi.mocked(api.apiGet).mockRejectedValueOnce(new Error("Network error"));
    const onError = vi.fn();
    const { result } = renderHook(() => useExecution("ex-1", { onError }));

    await act(async () => {
      result.current.loadExecution();
    });

    expect(onError).toHaveBeenCalledWith(expect.any(Error));
    expect(result.current.execution).toBeNull();
    expect(result.current.selectedNodeId).toBeNull();
  });

  it("cancelExecution 成功時に apiPost と onCancelSuccess を呼び、再取得する", async () => {
    const onCancelSuccess = vi.fn();
    const { result } = renderHook(() => useExecution("ex-1", { onCancelSuccess }));

    await act(async () => {
      result.current.loadExecution();
    });
    expect(result.current.execution).not.toBeNull();
    vi.mocked(api.apiGet).mockClear();

    await act(async () => {
      result.current.cancelExecution();
    });

    expect(api.apiPost).toHaveBeenCalledWith("/executions/ex-1/cancel", { reason: "ui" });
    expect(onCancelSuccess).toHaveBeenCalled();
    expect(api.apiGet).toHaveBeenCalledWith("/executions/ex-1");
  });

  it("cancelExecution 失敗時に onError を呼ぶ", async () => {
    const { result } = renderHook(() => useExecution("ex-1", { onError: vi.fn() }));
    await act(async () => {
      result.current.loadExecution();
    });
    vi.mocked(api.apiPost).mockRejectedValueOnce(new Error("Cancel failed"));
    const onError = vi.fn();
    const { result: result2 } = renderHook(() => useExecution("ex-1", { onError }));
    await act(async () => {
      result2.current.loadExecution();
    });

    await act(async () => {
      result2.current.cancelExecution();
    });

    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  describe("ストリーム・再接続", () => {
    const streamInstances: Array<{
      url: string;
      onopen: (() => void) | null;
      onerror: (() => void) | null;
      close: ReturnType<typeof vi.fn>;
    }> = [];

    class MockEventSource {
      url: string;
      onopen: (() => void) | null = null;
      onerror: (() => void) | null = null;
      onmessage: ((e: MessageEvent<string>) => void) | null = null;
      addEventListener = vi.fn();
      close = vi.fn();

      constructor(url: string) {
        this.url = url;
        streamInstances.push(this);
      }
    }

    beforeEach(() => {
      streamInstances.length = 0;
      vi.stubGlobal("EventSource", MockEventSource);
    });

    it("execution 取得後に EventSource が正しい URL で作成される", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });

      await waitFor(() => {
        expect(streamInstances.length).toBe(1);
      });
      expect(streamInstances[0]?.url).toBe("/api/core/executions/ex-1/stream");
    });

    it("ストリーム onerror で再接続がスケジュールされ、遅延後に再度 connect する", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      vi.useFakeTimers();
      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      expect(firstInstance.onerror).toBeDefined();
      act(() => {
        firstInstance.onerror?.();
      });
      expect(firstInstance.close).toHaveBeenCalled();

      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(streamInstances.length).toBe(2);
      expect(streamInstances[1]?.url).toBe("/api/core/executions/ex-1/stream");

      vi.useRealTimers();
    });

    it("再接続後の onopen で refreshExecutionSnapshot が呼ばれる", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      vi.mocked(api.apiGet).mockClear();
      act(() => {
        firstInstance.onopen?.();
      });
      expect(api.apiGet).not.toHaveBeenCalled();

      vi.useFakeTimers();
      act(() => {
        firstInstance.onerror?.();
      });
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(streamInstances.length).toBe(2);
      vi.useRealTimers();

      const secondInstance = streamInstances[1];
      if (!secondInstance) throw new Error("expected second stream instance");
      await act(async () => {
        secondInstance.onopen?.();
      });
      expect(api.apiGet).toHaveBeenCalledWith("/executions/ex-1");
    });

    it("アンマウント時にタイマーとストリームがクリーンアップされる", async () => {
      const { result, unmount } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      vi.useFakeTimers();
      act(() => {
        firstInstance.onerror?.();
      });
      expect(firstInstance.close).toHaveBeenCalled();

      unmount();
      act(() => {
        vi.advanceTimersByTime(10000);
      });
      expect(streamInstances.length).toBe(1);

      vi.useRealTimers();
    });
  });
});
