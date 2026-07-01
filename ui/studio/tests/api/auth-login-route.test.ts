import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { POST } from "../../app/api/auth/login/route";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../app/lib/authSession";
import { testJwt } from "../helpers/testJwt";

describe("auth login route", () => {
  const originalBase = process.env.CORE_API_INTERNAL_BASE;

  beforeEach(() => {
    process.env.CORE_API_INTERNAL_BASE = "http://core.test";
  });

  afterEach(() => {
    process.env.CORE_API_INTERNAL_BASE = originalBase;
    vi.unstubAllGlobals();
  });

  it("Core-API 成功時に Cookie を設定して ok を返す", async () => {
    const expiresAt = new Date(Date.now() + 3_600_000).toISOString();
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                accessToken: testJwt(Math.floor(Date.now() / 1000) + 3600),
                expiresAt,
                tenantId: "tid",
                tenantKey: "default",
                principalId: "pid"
              })
            )
        } as Response)
      )
    );

    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ tenantKey: "default", email: "u@example.com", password: "secret" })
    });
    const res = await POST(req);

    expect(res.status).toBe(200);
    const json: unknown = await res.json();
    expect(json).toEqual({ ok: true });
    expect(res.cookies.get(AUTH_COOKIE_ACCESS)?.value).toBeTruthy();
    expect(res.cookies.get(AUTH_COOKIE_TENANT_KEY)?.value).toBe("default");
    expect(fetch).toHaveBeenCalledWith(
      "http://core.test/v1/auth/login",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("不正 JSON は 422", async () => {
    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: "not-json"
    });
    const res = await POST(req);
    expect(res.status).toBe(422);
  });

  it("必須項目欠落は 422", async () => {
    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ tenantKey: "", email: "u@example.com", password: "" })
    });
    const res = await POST(req);
    expect(res.status).toBe(422);
  });

  it("上流エラーはステータスをそのまま返す", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: false,
          status: 401,
          text: () => Promise.resolve(JSON.stringify({ error: { code: "UNAUTHORIZED", message: "bad" } }))
        } as Response)
      )
    );

    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ tenantKey: "default", email: "u@example.com", password: "x" })
    });
    const res = await POST(req);
    expect(res.status).toBe(401);
  });

  it("不完全なログイン応答は 502", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          status: 200,
          text: () => Promise.resolve(JSON.stringify({ accessToken: "only" }))
        } as Response)
      )
    );

    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ tenantKey: "default", email: "u@example.com", password: "x" })
    });
    const res = await POST(req);
    expect(res.status).toBe(502);
  });

  it("上流が非 JSON 本文でも処理できる", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: false,
          status: 500,
          text: () => Promise.resolve("plain-error")
        } as Response)
      )
    );

    const req = new NextRequest("http://localhost/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ tenantKey: "default", email: "u@example.com", password: "x" })
    });
    const res = await POST(req);
    expect(res.status).toBe(500);
    expect(await res.json()).toBe("plain-error");
  });
});
