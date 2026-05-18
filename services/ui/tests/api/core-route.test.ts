import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { NextRequest } from "next/server";
import { GET } from "../../app/api/core/[...path]/route";

describe("api/core route GET", () => {
  const originalBase = process.env.CORE_API_INTERNAL_BASE;

  beforeEach(() => {
    process.env.CORE_API_INTERNAL_BASE = "http://core.test";
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          status: 200,
          headers: new Headers({ "content-type": "application/json" }),
          text: () => Promise.resolve("{}")
        } as Response)
      )
    );
  });

  afterEach(() => {
    process.env.CORE_API_INTERNAL_BASE = originalBase;
    vi.unstubAllGlobals();
  });

  it("workflows パスを v1/workflows に転送する", async () => {
    const req = new NextRequest("http://localhost/api/core/workflows/ex-1?limit=10");
    await GET(req, { params: Promise.resolve({ path: ["workflows", "ex-1"] }) });

    expect(fetch).toHaveBeenCalledWith(
      "http://core.test/v1/workflows/ex-1?limit=10",
      expect.objectContaining({ method: "GET" })
    );
  });

  it("stream パスではクエリ tenantId をヘッダに載せる", async () => {
    const req = new NextRequest("http://localhost/api/core/workflows/ex-1/stream?tenantId=t-1");
    await GET(req, { params: Promise.resolve({ path: ["workflows", "ex-1", "stream"] }) });

    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/v1/workflows/ex-1/stream"),
      expect.objectContaining({
        headers: expect.objectContaining({ "X-Tenant-Id": "t-1" })
      })
    );
  });

  it("204 レスポンスは本文なしで返す", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Headers({ "content-type": "application/json" }),
      text: () => Promise.resolve("")
    } as Response);

    const req = new NextRequest("http://localhost/api/core/workflows/ex-1");
    const res = await GET(req, { params: Promise.resolve({ path: ["workflows", "ex-1"] }) });

    expect(res.status).toBe(204);
    expect(await res.text()).toBe("");
  });
});
