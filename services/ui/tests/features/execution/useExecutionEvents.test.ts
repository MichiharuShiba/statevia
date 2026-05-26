import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { useExecutionEvents } from "../../../app/features/execution/useExecutionEvents";
import * as api from "../../../app/lib/api";
import type { ExecutionEventsResponse } from "../../../app/lib/types";

describe("useExecutionEvents", () => {
  beforeEach(() => {
    vi.spyOn(api, "apiGet");
  });

  it("executionId が null のときイベントは空", async () => {
    const { result } = renderHook(() => useExecutionEvents(null));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.events).toEqual([]);
    expect(result.current.hasMore).toBe(false);
    expect(api.apiGet).not.toHaveBeenCalled();
  });

  it("初回取得でイベント一覧を保持する", async () => {
    const response: ExecutionEventsResponse = {
      events: [{ seq: 1, type: "GraphUpdated", executionId: "ex-1", patch: { nodes: [] } }],
      hasMore: true
    };
    vi.mocked(api.apiGet).mockResolvedValue(response);

    const { result } = renderHook(() => useExecutionEvents("ex-1"));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.events).toHaveLength(1);
    expect(result.current.hasMore).toBe(true);
    expect(api.apiGet).toHaveBeenCalledWith(expect.stringContaining("/executions/ex-1/events"));
  });

  it("loadMore で afterSeq 付きの追加取得を行う", async () => {
    vi.mocked(api.apiGet)
      .mockResolvedValueOnce({
        events: [{ seq: 1, type: "ExecutionStatusChanged", executionId: "ex-1", to: "Running" }],
        hasMore: true
      })
      .mockResolvedValueOnce({
        events: [{ seq: 2, type: "GraphUpdated", executionId: "ex-1", patch: { nodes: [] } }],
        hasMore: false
      });

    const { result } = renderHook(() => useExecutionEvents("ex-1"));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    await act(async () => {
      result.current.loadMore();
    });

    await waitFor(() => {
      expect(result.current.loadingMore).toBe(false);
    });

    expect(result.current.events).toHaveLength(2);
    expect(result.current.hasMore).toBe(false);
    expect(api.apiGet).toHaveBeenLastCalledWith(expect.stringContaining("afterSeq=1"));
  });

  it("初回取得失敗時は error を保持し events を空にする", async () => {
    vi.mocked(api.apiGet).mockRejectedValue(new Error("network"));

    const { result } = renderHook(() => useExecutionEvents("ex-1"));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.events).toEqual([]);
  });
});
