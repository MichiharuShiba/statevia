import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiGet, apiPost, getApiConfig, getApiHeaders } from "../../app/lib/api";

describe("apiGet", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn((url: string) => {
        if (url.includes("/api/core/ok")) {
          return Promise.resolve({
            ok: true,
            statusText: "OK",
            text: () => Promise.resolve(JSON.stringify({ data: 1 }))
          } as Response);
        }
        if (url.includes("/api/core/empty")) {
          return Promise.resolve({
            ok: true,
            statusText: "OK",
            text: () => Promise.resolve("")
          } as Response);
        }
        if (url.includes("/api/core/error-json")) {
          return Promise.resolve({
            ok: false,
            status: 422,
            statusText: "Unprocessable Entity",
            text: () =>
              Promise.resolve(
                JSON.stringify({ error: { code: "INVALID_INPUT", message: "Validation failed" } })
              )
          } as Response);
        }
        if (url.includes("/api/core/error-plain")) {
          return Promise.resolve({
            ok: false,
            status: 500,
            statusText: "Internal Server Error",
            text: () => Promise.resolve("Plain error message")
          } as Response);
        }
        // error オブジェクトはあるが message が文字列でない → getErrorMessage が res.statusText を返す
        if (url.includes("/api/core/error-no-message")) {
          return Promise.resolve({
            ok: false,
            status: 400,
            statusText: "Bad Request",
            text: () => Promise.resolve(JSON.stringify({ error: { code: "BAD", message: 123 } }))
          } as Response);
        }
        // 不正な JSON で parseJsonSafe の catch を通す
        if (url.includes("/api/core/error-invalid-json")) {
          return Promise.resolve({
            ok: false,
            status: 502,
            statusText: "Bad Gateway",
            text: () => Promise.resolve("{ invalid")
          } as Response);
        }
        return Promise.reject(new Error("Unexpected URL"));
      })
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("成功時はパースした JSON を返す", async () => {
    // Arrange
    const path = "/ok";

    // Act
    const result = await apiGet<{ data: number }>(path);

    // Assert
    expect(result).toEqual({ data: 1 });
    expect(fetch).toHaveBeenCalledWith("/api/core/ok", expect.objectContaining({ cache: "no-store" }));
  });

  it("レスポンス body が空文字のとき null を返す（parseJsonSafe の t ? 偽）", async () => {
    const result = await apiGet<unknown>("/empty");
    expect(result).toBeNull();
  });

  it("JSON のエラー応答のとき status と error body で ApiError をスローする", async () => {
    // Arrange
    const path = "/error-json";

    // Act / Assert
    await expect(apiGet(path)).rejects.toMatchObject({
      status: 422,
      error: { code: "INVALID_INPUT", message: "Validation failed" }
    });
  });

  it("レスポンスが JSON でないとき生テキストで ApiError をスローする", async () => {
    // Arrange
    const path = "/error-plain";

    // Act / Assert
    await expect(apiGet(path)).rejects.toMatchObject({
      status: 500,
      error: { code: "HTTP_500", message: "Plain error message" }
    });
  });

  it("error.message が文字列でないとき statusText を使う", async () => {
    // Arrange
    const path = "/error-no-message";

    // Act / Assert
    await expect(apiGet(path)).rejects.toMatchObject({
      status: 400,
      error: { code: "BAD", message: "Bad Request" }
    });
  });

  it("不正な JSON レスポンスのとき message に生テキストを使う", async () => {
    // Arrange
    const path = "/error-invalid-json";

    // Act / Assert
    await expect(apiGet(path)).rejects.toMatchObject({
      status: 502,
      error: { code: "HTTP_502", message: "{ invalid" }
    });
  });
});

describe("apiPost", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn((_url: string, init?: RequestInit) => {
        const body = init?.body as string | undefined;
        const idem = init?.headers && (init.headers as Record<string, string>)["X-Idempotency-Key"];
        return Promise.resolve({
          ok: true,
          statusText: "OK",
          text: () => Promise.resolve(JSON.stringify({ accepted: true, idem, body: body ? JSON.parse(body) : {} }))
        } as Response);
      })
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("POST で JSON body と idempotency key を送る", async () => {
    // Arrange
    const path = "/workflows/ex-1/nodes/n-1/resume";
    const body = { resumeKey: "key-1" };

    // Act
    const result = await apiPost<{ accepted: boolean; idem: string; body: unknown }>(path, body);

    // Assert
    expect(result.accepted).toBe(true);
    expect(result.body).toEqual(body);
    expect(typeof result.idem).toBe("string");
    expect(fetch).toHaveBeenCalledWith(
      "/api/core/workflows/ex-1/nodes/n-1/resume",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          "Content-Type": "application/json",
          "X-Idempotency-Key": expect.any(String)
        }),
        body: JSON.stringify(body)
      })
    );
  });

  it("body が null/undefined のとき空オブジェクトを送る", async () => {
    // Arrange
    const path = "/ping";

    // Act
    const result = await apiPost<{ body: unknown }>(path, null);

    // Assert
    expect(result.body).toEqual({});
  });
});

describe("api (境界値)", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          statusText: "OK",
          text: () => Promise.resolve(JSON.stringify({ ok: true }))
        } as Response)
      )
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("path が空文字のとき /api/core に GET する", async () => {
    // Arrange
    const path = "";

    // Act
    await apiGet(path);

    // Assert
    expect(fetch).toHaveBeenCalledWith("/api/core", expect.any(Object));
  });

  it("apiPost で body が undefined のとき JSON は {}", async () => {
    // Arrange
    const path = "/test";

    // Act
    await apiPost(path, undefined);

    // Assert
    expect(fetch).toHaveBeenCalledWith(
      "/api/core/test",
      expect.objectContaining({ body: "{}" })
    );
  });
});

describe("getApiConfig / getApiHeaders", () => {
  const origEnv = process.env;

  afterEach(() => {
    process.env = origEnv;
  });

  it("getApiConfig は NEXT_PUBLIC_TENANT_ID と NEXT_PUBLIC_AUTH_TOKEN を返す", () => {
    // Arrange
    process.env = { ...origEnv, NEXT_PUBLIC_TENANT_ID: "t1", NEXT_PUBLIC_AUTH_TOKEN: "tok1" };

    // Act
    const result = getApiConfig();

    // Assert
    expect(result).toEqual({ tenantId: "t1", authToken: "tok1" });
  });

  it("getApiHeaders は tenantId のみのとき X-Tenant-Id だけ返す", () => {
    // Arrange
    process.env = { ...origEnv, NEXT_PUBLIC_TENANT_ID: "t2", NEXT_PUBLIC_AUTH_TOKEN: "" };

    // Act
    const result = getApiHeaders();

    // Assert
    expect(result).toEqual({ "X-Tenant-Id": "t2" });
  });

  it("getApiHeaders は authToken があるとき Authorization Bearer を返す", () => {
    // Arrange
    process.env = { ...origEnv, NEXT_PUBLIC_TENANT_ID: "", NEXT_PUBLIC_AUTH_TOKEN: "secret" };

    // Act
    const result = getApiHeaders();

    // Assert
    expect(result).toEqual({ Authorization: "Bearer secret" });
  });

  it("getApiConfig は process が無い環境で空の設定を返す", () => {
    // Arrange
    const origProcess = (globalThis as unknown as { process?: unknown }).process;
    (globalThis as unknown as { process?: unknown }).process = undefined;

    // Act
    const result = getApiConfig();

    // Assert
    expect(result).toEqual({ tenantId: "", authToken: "" });

    // Cleanup
    (globalThis as unknown as { process?: unknown }).process = origProcess;
  });
});
