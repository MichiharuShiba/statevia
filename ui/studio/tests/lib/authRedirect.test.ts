import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { buildLoginRedirectUrl, clearSessionAndRedirectToLogin } from "../../app/lib/authRedirect";

describe("buildLoginRedirectUrl", () => {
  it("from を付与する", () => {
    const url = buildLoginRedirectUrl("/dashboard", "http://localhost:3000");
    expect(url).toBe("http://localhost:3000/login?from=%2Fdashboard");
  });

  it("origin 省略時はブラウザ origin または localhost をベースにする", () => {
    const url = buildLoginRedirectUrl("/executions");
    const expectedOrigin =
      "location" in globalThis && globalThis.location !== undefined
        ? globalThis.location.origin
        : "http://localhost";
    expect(url).toBe(`${expectedOrigin}/login?from=%2Fexecutions`);
  });

  it("/login と外部 URL 風パスには from を付けない", () => {
    expect(buildLoginRedirectUrl("/login", "http://localhost:3000")).toBe("http://localhost:3000/login");
    expect(buildLoginRedirectUrl("//evil", "http://localhost:3000")).toBe("http://localhost:3000/login");
    expect(buildLoginRedirectUrl("/%2F%2Fevil.com", "http://localhost:3000")).toBe(
      "http://localhost:3000/login"
    );
    expect(buildLoginRedirectUrl(String.raw`/\@evil`, "http://localhost:3000")).toBe(
      "http://localhost:3000/login"
    );
  });
});

describe("clearSessionAndRedirectToLogin", () => {
  const assign = vi.fn();

  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.resolve({ ok: true } as Response))
    );
    Object.defineProperty(globalThis, "location", {
      configurable: true,
      writable: true,
      value: {
        pathname: "/dashboard",
        search: "?tab=1",
        origin: "http://localhost:3000",
        assign
      }
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    assign.mockReset();
  });

  it("location が無い環境では何もしない", async () => {
    const originalLocation = globalThis.location;
    // @ts-expect-error テスト用に location を除去
    delete globalThis.location;

    await clearSessionAndRedirectToLogin();

    expect(fetch).not.toHaveBeenCalled();
    expect(assign).not.toHaveBeenCalled();

    globalThis.location = originalLocation;
  });

  it("ログアウト API を呼んでログイン URL へ遷移する", async () => {
    await clearSessionAndRedirectToLogin();

    expect(fetch).toHaveBeenCalledWith("/api/auth/logout", {
      method: "POST",
      credentials: "same-origin"
    });
    expect(assign).toHaveBeenCalledWith("http://localhost:3000/login?from=%2Fdashboard%3Ftab%3D1");
  });
});
