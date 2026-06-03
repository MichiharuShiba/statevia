import { afterEach, describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { AUTH_COOKIE_ACCESS } from "../app/lib/authSession";
import { middleware } from "../middleware";
import { testJwt } from "./helpers/testJwt";

function requestWithCookie(path: string, accessToken?: string): NextRequest {
  const headers: Record<string, string> = {};
  if (accessToken) {
    headers.cookie = `${AUTH_COOKIE_ACCESS}=${accessToken}`;
  }
  return new NextRequest(`http://localhost:3000${path}`, { headers });
}

describe("middleware", () => {
  const originalAuthToken = process.env.CORE_API_AUTH_TOKEN;

  afterEach(() => {
    if (originalAuthToken === undefined) {
      delete process.env.CORE_API_AUTH_TOKEN;
    } else {
      process.env.CORE_API_AUTH_TOKEN = originalAuthToken;
    }
  });

  it("未ログインで保護パスは /login へリダイレクトする", () => {
    delete process.env.CORE_API_AUTH_TOKEN;
    const res = middleware(requestWithCookie("/dashboard"));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toContain("/login?from=%2Fdashboard");
  });

  it("期限切れトークンは /login へリダイレクトし Cookie を削除する", () => {
    delete process.env.CORE_API_AUTH_TOKEN;
    const expired = testJwt(Math.floor(Date.now() / 1000) - 120);
    const res = middleware(requestWithCookie("/executions", expired));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toContain("/login");
    expect(res.cookies.get(AUTH_COOKIE_ACCESS)?.value ?? "").toBe("");
  });

  it("有効なトークンは通過する", () => {
    delete process.env.CORE_API_AUTH_TOKEN;
    const valid = testJwt(Math.floor(Date.now() / 1000) + 3600);
    const res = middleware(requestWithCookie("/dashboard", valid));
    expect(res.status).toBe(200);
    expect(res.headers.get("location")).toBeNull();
  });

  it("/login は公開パスとして通過する", () => {
    delete process.env.CORE_API_AUTH_TOKEN;
    const res = middleware(requestWithCookie("/login"));
    expect(res.status).toBe(200);
  });
});
