import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useExecution, getReconnectDelayMs } from "../../../app/features/execution/useExecution";
import type { WorkflowDTO, WorkflowGraphDTO } from "../../../app/lib/types";
import * as api from "../../../app/lib/api";

function workflowDto(overrides: Partial<WorkflowDTO> = {}): WorkflowDTO {
  return {
    displayId: "ex-1",
    resourceId: "r-1",
    status: "Running",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    ...overrides
  };
}

function graphDto(nodes: Array<{ nodeId: string; stateName: string; completedAt?: string | null }> = []): WorkflowGraphDTO {
  return {
    nodes: nodes.map((n) => ({
      nodeId: n.nodeId,
      stateName: n.stateName,
      startedAt: "2026-01-01T00:00:00Z",
      completedAt: n.completedAt ?? null
    })),
    edges: []
  };
}

/** v2: apiGet を workflow と graph の2回呼び出し用にモックする。 */
function mockApiGetForWorkflowAndGraph(workflow: WorkflowDTO, graph: WorkflowGraphDTO) {
  let callIndex = 0;
  vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
    if (path.includes("/graph")) return graph as never;
    callIndex++;
    return workflow as never;
  });
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

  it("境界値: baseMs が 0 のとき 0 を返す", () => {
    expect(getReconnectDelayMs(0, 0, 30000)).toBe(0);
    expect(getReconnectDelayMs(3, 0, 30000)).toBe(0);
  });

  it("境界値: maxMs が 0 のとき常に 0 を返す", () => {
    expect(getReconnectDelayMs(0, 1000, 0)).toBe(0);
    expect(getReconnectDelayMs(2, 1000, 0)).toBe(0);
  });
});

/** jsdom に EventSource がないため、全 useExecution テストでスタブする最小クラス */
class MinimalEventSource {
  close = vi.fn();
  addEventListener = vi.fn();
  constructor(public readonly url: string) {}
}

describe("useExecution", () => {
  const defaultWorkflow = workflowDto();
  const defaultGraph = graphDto([{ nodeId: "n-1", stateName: "TASK" }]);

  beforeEach(() => {
    vi.stubGlobal("EventSource", MinimalEventSource);
    vi.spyOn(api, "apiGet");
    mockApiGetForWorkflowAndGraph(defaultWorkflow, defaultGraph);
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
    // Arrange
    const { result } = renderHook(() => useExecution("ex-1"));

    // Assert (初期)
    expect(result.current.execution).toBeNull();

    // Act
    await act(async () => {
      result.current.loadExecution();
    });

    // Assert
    expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1");
    expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1/graph");
    expect(result.current.execution).not.toBeNull();
    expect(result.current.execution?.displayId).toBe("ex-1");
    expect(result.current.selectedNodeId).toBe("n-1");
  });

  it("loadExecution 失敗時に onError を呼び、execution を null にする", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockRejectedValueOnce(new Error("Network error"));
    const onError = vi.fn();
    const { result } = renderHook(() => useExecution("ex-1", { onError }));

    // Act
    await act(async () => {
      result.current.loadExecution();
    });

    // Assert
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
    expect(result.current.execution).toBeNull();
    expect(result.current.selectedNodeId).toBeNull();
  });

  it("cancelExecution 成功時に apiPost と onCancelSuccess を呼び、再取得する", async () => {
    // Arrange
    const onCancelSuccess = vi.fn();
    const { result } = renderHook(() => useExecution("ex-1", { onCancelSuccess }));
    await act(async () => {
      result.current.loadExecution();
    });
    expect(result.current.execution).not.toBeNull();
    vi.mocked(api.apiGet).mockClear();
    mockApiGetForWorkflowAndGraph(defaultWorkflow, defaultGraph);

    // Act
    await act(async () => {
      result.current.cancelExecution();
    });

    // Assert
    expect(api.apiPost).toHaveBeenCalledWith("/workflows/ex-1/cancel", { reason: "ui" });
    expect(onCancelSuccess).toHaveBeenCalled();
    expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1");
  });

  it("cancelExecution で POST 成功後に再取得が失敗したとき onCancelSuccess を呼ばず onError とクリアする", async () => {
    const onCancelSuccess = vi.fn();
    const onError = vi.fn();
    const { result } = renderHook(() => useExecution("ex-1", { onCancelSuccess, onError }));
    await act(async () => {
      result.current.loadExecution();
    });
    expect(result.current.execution).not.toBeNull();
    vi.mocked(api.apiGet).mockRejectedValueOnce(new Error("refresh failed"));

    await act(async () => {
      result.current.cancelExecution();
    });

    expect(onCancelSuccess).not.toHaveBeenCalled();
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
    expect(result.current.execution).toBeNull();
  });

  it("cancelExecution 失敗時に onError を呼ぶ", async () => {
    // Arrange
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

    // Act
    await act(async () => {
      result2.current.cancelExecution();
    });

    // Assert
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  describe("ストリーム・再接続", () => {
    const streamInstances: Array<{
      url: string;
      onopen: (() => void) | null;
      onerror: (() => void) | null;
      onmessage: ((e: MessageEvent<string>) => void) | null;
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
      vi.spyOn(api, "getApiConfig").mockReturnValue({ tenantId: "", authToken: "" });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("execution 取得後に EventSource が正しい URL で作成される", async () => {
      // Arrange (getApiConfig は beforeEach で空テナントにモック済み)
      const { result } = renderHook(() => useExecution("ex-1"));

      // Act
      await act(async () => {
        result.current.loadExecution();
      });

      // Assert
      await waitFor(() => {
        expect(streamInstances.length).toBe(1);
      });
      expect(streamInstances[0]?.url).toBe("/api/core/workflows/ex-1/stream");
    });

    it("getApiConfig で tenantId があるとき EventSource の URL に tenantId クエリが付く", async () => {
      // Arrange
      vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "tenant-a", authToken: "" });
      const { result } = renderHook(() => useExecution("ex-1"));

      // Act
      await act(async () => {
        result.current.loadExecution();
      });

      // Assert
      await waitFor(() => {
        expect(streamInstances.length).toBe(1);
      });
      expect(streamInstances[0]?.url).toBe(
        "/api/core/workflows/ex-1/stream?tenantId=tenant-a"
      );
    });

    it("streamEnabled false のとき EventSource は作成されない", async () => {
      const { result } = renderHook(() => useExecution("ex-1", { streamEnabled: false }));

      await act(async () => {
        await result.current.loadExecution();
      });

      expect(streamInstances.length).toBe(0);
    });

    it("streamEnabled false のとき Running 中はポーリング間隔で再取得する", async () => {
      vi.useFakeTimers();
      const { result } = renderHook(() => useExecution("ex-1", { streamEnabled: false }));

      await act(async () => {
        await result.current.loadExecution();
      });

      vi.mocked(api.apiGet).mockClear();
      mockApiGetForWorkflowAndGraph(defaultWorkflow, defaultGraph);

      await act(async () => {
        vi.advanceTimersByTime(2500);
      });

      expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1");
      expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1/graph");

      vi.useRealTimers();
    });

    it("ストリーム onmessage 後にデバウンス経過で GET が走る", async () => {
      const { result } = renderHook(() => useExecution("ex-1", { streamRefreshDebounceMs: 50 }));

      await act(async () => {
        await result.current.loadExecution();
      });

      await waitFor(() => expect(streamInstances.length).toBe(1));

      vi.mocked(api.apiGet).mockClear();
      mockApiGetForWorkflowAndGraph(defaultWorkflow, defaultGraph);

      const firstInstance = streamInstances[0];
      const onmessage = firstInstance?.onmessage;
      if (!onmessage) throw new Error("expected onmessage");

      const graphUpdated = JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] }
      });

      await act(async () => {
        onmessage({ data: graphUpdated } as MessageEvent<string>);
      });

      expect(api.apiGet).not.toHaveBeenCalled();

      await waitFor(
        () => {
          expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1");
          expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1/graph");
        },
        { timeout: 2000 }
      );
    });

    it("onmessage でパースできないデータのとき applyRawEvent は早期 return する", async () => {
      // Arrange
      const { result } = renderHook(() => useExecution("ex-1"));
      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));
      const firstInstance = streamInstances[0];
      if (!firstInstance?.onmessage) throw new Error("expected onmessage");

      // Act: 不正な JSON を送っても例外にならない
      act(() => {
        firstInstance.onmessage?.({ data: "not valid json" } as MessageEvent<string>);
      });

      // Assert: execution は変わらない（applyRawEvent が early return した）
      expect(result.current.execution?.displayId).toBe("ex-1");
    });

    it("ストリーム onerror で再接続がスケジュールされ、遅延後に再度 connect する", async () => {
      // Arrange
      const { result } = renderHook(() => useExecution("ex-1"));
      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));
      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      vi.useFakeTimers();

      // Act
      act(() => {
        firstInstance.onerror?.();
      });

      // Assert
      expect(firstInstance.close).toHaveBeenCalled();
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(streamInstances.length).toBe(2);
      expect(streamInstances[1]?.url).toBe("/api/core/workflows/ex-1/stream");

      vi.useRealTimers();
    });

    it("再接続後の onopen で refreshExecutionSnapshot が呼ばれる", async () => {
      // Arrange
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

      // Act: 再接続後の onopen
      const secondInstance = streamInstances[1];
      if (!secondInstance) throw new Error("expected second stream instance");
      await act(async () => {
        secondInstance.onopen?.();
      });

      // Assert
      expect(api.apiGet).toHaveBeenCalledWith("/workflows/ex-1");
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

    it("ストリーム onmessage で有効な GraphUpdated を受信すると execution が更新される", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      const onmessage = firstInstance?.onmessage;
      if (!onmessage) throw new Error("expected onmessage handler");
      const graphUpdated = JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] }
      });

      await act(async () => {
        onmessage({ data: graphUpdated } as MessageEvent<string>);
      });

      expect(result.current.execution?.nodes[0]?.status).toBe("RUNNING");
    });

    it("ストリーム onmessage で無効な JSON のときは execution を更新しない", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      const onmessage = firstInstance?.onmessage;
      if (!onmessage) throw new Error("expected onmessage handler");
      const beforeStatus = result.current.execution?.nodes[0]?.status;

      act(() => {
        onmessage({ data: "" } as MessageEvent<string>);
      });
      act(() => {
        onmessage({ data: "not json" } as MessageEvent<string>);
      });

      expect(result.current.execution?.nodes[0]?.status).toBe(beforeStatus);
    });

    it("再接続後の refreshExecutionSnapshot が失敗してもストリームは維持される", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      act(() => {
        firstInstance.onopen?.();
      });
      vi.useFakeTimers();
      act(() => {
        firstInstance.onerror?.();
      });
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(streamInstances.length).toBe(2);
      vi.useRealTimers();

      vi.mocked(api.apiGet).mockRejectedValueOnce(new Error("Refresh failed"));
      const secondInstance = streamInstances[1];
      if (!secondInstance) throw new Error("expected second stream instance");
      await act(async () => {
        secondInstance.onopen?.();
      });
      expect(result.current.execution).not.toBeNull();
    });

    it("アンマウント後に onerror が呼ばれても disposed のため何もしない", async () => {
      const { result, unmount } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      const onerror = firstInstance.onerror;

      unmount();
      act(() => {
        onerror?.();
      });
      expect(streamInstances.length).toBe(1);
    });

    it("2回目以降の onerror でも scheduleReconnect と clearReconnectTimer が正しく動く", async () => {
      const { result } = renderHook(() => useExecution("ex-1"));

      await act(async () => {
        result.current.loadExecution();
      });
      await waitFor(() => expect(streamInstances.length).toBe(1));

      vi.useFakeTimers();
      const firstInstance = streamInstances[0];
      if (!firstInstance) throw new Error("expected one stream instance");
      act(() => {
        firstInstance.onerror?.();
      });
      act(() => {
        vi.advanceTimersByTime(1000);
      });
      expect(streamInstances.length).toBe(2);

      const secondInstance = streamInstances[1];
      if (!secondInstance) throw new Error("expected second stream instance");
      act(() => {
        secondInstance.onerror?.();
      });
      act(() => {
        vi.advanceTimersByTime(2000);
      });
      expect(streamInstances.length).toBe(3);

      vi.useRealTimers();
    });
  });

  it("大量ノードで loadExecution 時 selectedNodeId は先頭ノードになる", async () => {
    const manyGraphNodes = Array.from({ length: 200 }, (_, i) => ({
      nodeId: `n-${i}`,
      stateName: "TASK" as const
    }));
    mockApiGetForWorkflowAndGraph(workflowDto(), graphDto(manyGraphNodes));

    const { result } = renderHook(() => useExecution("ex-1"));

    await act(async () => {
      result.current.loadExecution();
    });

    expect(result.current.execution?.nodes).toHaveLength(200);
    expect(result.current.selectedNodeId).toBe("n-0");
  });

  it("execution が null のとき cancelExecution は何もしない", async () => {
    const { result } = renderHook(() => useExecution("ex-1"));

    await act(async () => {
      result.current.cancelExecution();
    });

    expect(api.apiPost).not.toHaveBeenCalled();
  });

  it("applyExecutionSnapshot: nodes が空のとき selectedNodeId は null", async () => {
    mockApiGetForWorkflowAndGraph(workflowDto(), graphDto([]));

    const { result } = renderHook(() => useExecution("ex-1"));

    await act(async () => {
      result.current.loadExecution();
    });

    expect(result.current.execution?.nodes).toHaveLength(0);
    expect(result.current.selectedNodeId).toBeNull();
  });

  it("applyExecutionSnapshot: 現在の selectedNodeId が response に無いとき先頭ノードに切り替わる", async () => {
    mockApiGetForWorkflowAndGraph(workflowDto(), graphDto([{ nodeId: "n-1", stateName: "TASK" }]));

    const { result } = renderHook(() => useExecution("ex-1"));

    await act(async () => {
      result.current.loadExecution();
    });
    expect(result.current.selectedNodeId).toBe("n-1");

    act(() => {
      result.current.setSelectedNodeId("n-1");
    });

    mockApiGetForWorkflowAndGraph(
      workflowDto(),
      graphDto([{ nodeId: "n-2", stateName: "TASK" }, { nodeId: "n-3", stateName: "TASK" }])
    );
    await act(async () => {
      result.current.loadExecution();
    });
    expect(result.current.selectedNodeId).toBe("n-2");
  });

  it("アンマウント時は clearReconnectTimer が timer 未設定でも安全に実行される", async () => {
    const { result, unmount } = renderHook(() => useExecution("ex-1"));

    await act(async () => {
      result.current.loadExecution();
    });
    unmount();
  });
});
