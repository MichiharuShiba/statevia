import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useExecutionStateAtSeq } from "../../../app/features/execution/useExecutionStateAtSeq";
import * as api from "../../../app/lib/api";
import type { ExecutionView } from "../../../app/lib/types";

describe("useExecutionStateAtSeq", () => {
  beforeEach(() => {
    vi.spyOn(api, "apiGet");
  });

  it("atSeq が null のとき state は null", async () => {
    const { result } = renderHook(() => useExecutionStateAtSeq("ex-1", null));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.state).toBeNull();
    expect(api.apiGet).not.toHaveBeenCalled();
  });

  it("atSeq 指定時は state エンドポイントを取得する", async () => {
    const view = { displayId: "ex-1", status: "Running" } as ExecutionView;
    vi.mocked(api.apiGet).mockResolvedValue(view);

    const { result } = renderHook(() => useExecutionStateAtSeq("ex-1", 3));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.state).toEqual(view);
    expect(api.apiGet).toHaveBeenCalledWith("/executions/ex-1/state?atSeq=3");
  });

  it("取得失敗時は error を保持し state を null にする", async () => {
    vi.mocked(api.apiGet).mockRejectedValue(new Error("not found"));

    const { result } = renderHook(() => useExecutionStateAtSeq("ex-1", 2));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.state).toBeNull();
  });
});
