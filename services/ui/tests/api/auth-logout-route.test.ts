import { describe, expect, it } from "vitest";
import { POST } from "../../app/api/auth/logout/route";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../app/lib/authSession";

describe("auth logout route", () => {
  it("POST でセッション Cookie を削除する", () => {
    const res = POST();
    expect(res.status).toBe(200);
    expect(res.cookies.get(AUTH_COOKIE_ACCESS)?.value ?? "").toBe("");
    expect(res.cookies.get(AUTH_COOKIE_TENANT_KEY)?.value ?? "").toBe("");
  });
});

