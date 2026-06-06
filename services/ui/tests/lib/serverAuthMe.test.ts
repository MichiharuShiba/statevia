import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../app/lib/authSession";
import { fetchAuthMeServer } from "../../app/lib/serverAuthMe";
import { testJwt } from "../helpers/testJwt";

const cookiesMock = vi.fn();

vi.mock("next/headers", () => ({
  cookies: () => cookiesMock()
}));

/** `next/headers` の cookies モックを設定する。 */
function mockCookieStore(access?: string, tenantKey?: string) {
  cookiesMock.mockResolvedValue({
    get: (name: string) => {
      if (name === AUTH_COOKIE_ACCESS && access) return { value: access };
      if (name === AUTH_COOKIE_TENANT_KEY && tenantKey) return { value: tenantKey };
      return undefined;
    }
  });
}

describe("fetchAuthMeServer", () => {
  const originalBase = process.env.CORE_API_INTERNAL_BASE;

  beforeEach(() => {
    process.env.CORE_API_INTERNAL_BASE = "http://core-api.test/";
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    process.env.CORE_API_INTERNAL_BASE = originalBase;
    vi.unstubAllGlobals();
    cookiesMock.mockReset();
  });

  it("アクセス Cookie が無いとき null を返す", async () => {
    mockCookieStore();

    await expect(fetchAuthMeServer()).resolves.toBeNull();
    expect(fetch).not.toHaveBeenCalled();
  });

  it("Core-API から Principal 情報を返す", async () => {
    mockCookieStore("jwt-token", "default");
    vi.mocked(fetch).mockResolvedValue(
      Response.json({
        tenantId: "tenant",
        tenantKey: "default",
        principalId: "principal",
        email: "admin@example.com",
        isTenantAdmin: true
      })
    );

    const me = await fetchAuthMeServer();

    expect(me?.email).toBe("admin@example.com");
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

  it("401 のとき null を返す", async () => {
    mockCookieStore(testJwt(Math.floor(Date.now() / 1000) + 3600));
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 401 }));

    await expect(fetchAuthMeServer()).resolves.toBeNull();
  });

  it("不正な JSON のとき null を返す", async () => {
    mockCookieStore(testJwt(Math.floor(Date.now() / 1000) + 3600));
    vi.mocked(fetch).mockResolvedValue(
      Response.json({ unexpected: true })
    );

    await expect(fetchAuthMeServer()).resolves.toBeNull();
  });
});
