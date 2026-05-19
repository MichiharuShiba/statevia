import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useGraphDefinition } from "../../../app/features/graph/useGraphDefinition";
import * as api from "../../../app/lib/api";

describe("useGraphDefinition", () => {
  beforeEach(() => {
    vi.spyOn(api, "apiGet");
  });

  it("graphId が null のとき definition は null", async () => {
    const { result } = renderHook(() => useGraphDefinition(null));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.definition).toBeNull();
    expect(result.current.source).toBe("none");
    expect(api.apiGet).not.toHaveBeenCalled();
  });

  it("API が有効な定義を返すとき source は api", async () => {
    vi.mocked(api.apiGet).mockResolvedValue({
      graphId: "hello",
      nodes: [{ nodeId: "n1", nodeType: "TASK" }],
      edges: []
    });

    const { result } = renderHook(() => useGraphDefinition("hello"));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.source).toBe("api");
    expect(result.current.definition?.graphId).toBe("hello");
  });

  it("API 失敗時は registry にフォールバックする", async () => {
    vi.mocked(api.apiGet).mockRejectedValue(new Error("404"));

    const { result } = renderHook(() => useGraphDefinition("hello"));

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.source).toBe("registry");
    expect(result.current.definition?.graphId).toBe("hello");
    expect(result.current.error).toBeInstanceOf(Error);
  });
});
