import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { NextRequest } from "next/server";
import { GET } from "../../app/api/auth/me/route";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../app/lib/authSession";

describe("GET /api/auth/me", () => {
  const originalBase = process.env.CORE_API_INTERNAL_BASE;

  beforeEach(() => {
    process.env.CORE_API_INTERNAL_BASE = "http://core-api.test";
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          tenantId: "00000000-0000-4000-8000-000000000001",
          tenantKey: "default",
          principalId: "00000000-0000-4000-8000-000000000099",
          email: "admin@example.com",
          isTenantAdmin: true
        })
      )
    );
  });

  afterEach(() => {
    process.env.CORE_API_INTERNAL_BASE = originalBase;
    vi.unstubAllGlobals();
  });

  it("アクセス Cookie が無いとき 401 を返す", async () => {
    const req = new NextRequest("http://localhost/api/auth/me");
    const res = await GET(req);
    expect(res.status).toBe(401);
  });

  it("上流が不正 JSON のとき INVALID_RESPONSE を返す", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response("not-json", { status: 200 }))
    );
    const req = new NextRequest("http://localhost/api/auth/me");
    req.cookies.set(AUTH_COOKIE_ACCESS, "jwt-token");

    const res = await GET(req);
    const body: { error?: { code?: string } } = await res.json();

    expect(res.status).toBe(200);
    expect(body.error?.code).toBe("INVALID_RESPONSE");
  });

  it("Bearer とテナントヘッダー付きで Core-API にプロキシする", async () => {
    const req = new NextRequest("http://localhost/api/auth/me");
    req.cookies.set(AUTH_COOKIE_ACCESS, "jwt-token");
    req.cookies.set(AUTH_COOKIE_TENANT_KEY, "default");
    const res = await GET(req);
    expect(res.status).toBe(200);
    const body: { isTenantAdmin?: boolean } = await res.json();
    expect(body.isTenantAdmin).toBe(true);
    expect(fetch).toHaveBeenCalledWith(
      "http://core-api.test/v1/auth/me",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer jwt-token",
          "X-Tenant-Id": "default"
        })
      })
    );
  });
});
