import { describe, expect, it, vi, beforeEach } from "vitest";
import { fetchAuthMe } from "../../app/lib/fetchAuthMe";

describe("fetchAuthMe", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: () =>
            Promise.resolve({
              tenantId: "tenant",
              tenantKey: "default",
              principalId: "principal",
              email: "admin@example.com",
              isTenantAdmin: true
            })
        })
      )
    );
  });

  it("認証済み Principal 情報を返す", async () => {
    const me = await fetchAuthMe();
    expect(me?.email).toBe("admin@example.com");
    expect(me?.isTenantAdmin).toBe(true);
  });

  it("isTenantAdmin を含まない JSON のとき null を返す", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ email: "admin@example.com" })
        })
      )
    );

    await expect(fetchAuthMe()).resolves.toBeNull();
  });

  it("401 のとき null を返す", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: false,
          status: 401
        })
      )
    );

    await expect(fetchAuthMe()).resolves.toBeNull();
  });
});
