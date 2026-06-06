import { describe, expect, it, afterEach } from "vitest";
import { NextRequest } from "next/server";
import { GET } from "../../app/api/auth/session/route";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../app/lib/authSession";
import { testJwt } from "../helpers/testJwt";

describe("GET /api/auth/session", () => {
  const originalAuthToken = process.env.CORE_API_AUTH_TOKEN;
  const originalTenantId = process.env.CORE_API_TENANT_ID;

  afterEach(() => {
    process.env.CORE_API_AUTH_TOKEN = originalAuthToken;
    process.env.CORE_API_TENANT_ID = originalTenantId;
  });

  it("未認証セッションでは authenticated が false", async () => {
    const req = new NextRequest("http://localhost/api/auth/session");
    const res = GET(req);
    const body: { authenticated: boolean; tenantKey: string } = await res.json();

    expect(body.authenticated).toBe(false);
    expect(body.tenantKey).toBe("");
  });

  it("有効な JWT Cookie では authenticated が true", async () => {
    const req = new NextRequest("http://localhost/api/auth/session");
    req.cookies.set(AUTH_COOKIE_ACCESS, testJwt(Math.floor(Date.now() / 1000) + 3600));
    req.cookies.set(AUTH_COOKIE_TENANT_KEY, "default");

    const body: { authenticated: boolean; tenantKey: string } = await GET(req).json();

    expect(body.authenticated).toBe(true);
    expect(body.tenantKey).toBe("default");
  });

  it("テナント Cookie が無いとき CORE_API_TENANT_ID を返す", async () => {
    process.env.CORE_API_TENANT_ID = "env-tenant";
    const req = new NextRequest("http://localhost/api/auth/session");
    req.cookies.set(AUTH_COOKIE_ACCESS, testJwt(Math.floor(Date.now() / 1000) + 3600));

    const body: { tenantKey: string } = await GET(req).json();

    expect(body.tenantKey).toBe("env-tenant");
  });
});
