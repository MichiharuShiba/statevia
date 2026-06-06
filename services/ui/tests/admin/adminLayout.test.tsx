import { describe, expect, it, vi, beforeEach } from "vitest";
import AdminLayout from "../../app/admin/layout";
import { fetchAuthMeServer } from "../../app/lib/serverAuthMe";
import { redirect } from "next/navigation";

vi.mock("../../app/lib/serverAuthMe", () => ({
  fetchAuthMeServer: vi.fn()
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn((url: string) => {
    throw new Error(`REDIRECT:${url}`);
  })
}));

describe("AdminLayout", () => {
  beforeEach(() => {
    vi.mocked(redirect).mockClear();
  });

  it("未認証時はログインへリダイレクトする", async () => {
    vi.mocked(fetchAuthMeServer).mockResolvedValue(null);

    await expect(AdminLayout({ children: <div>content</div> })).rejects.toThrow(
      "REDIRECT:/login?from=/admin/users"
    );
    expect(redirect).toHaveBeenCalledWith("/login?from=/admin/users");
  });

  it("テナント管理者以外はダッシュボードへリダイレクトする", async () => {
    vi.mocked(fetchAuthMeServer).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      email: "user@example.com",
      isTenantAdmin: false
    });

    await expect(AdminLayout({ children: <div>content</div> })).rejects.toThrow("REDIRECT:/dashboard");
    expect(redirect).toHaveBeenCalledWith("/dashboard");
  });

  it("テナント管理者には children を描画する", async () => {
    vi.mocked(fetchAuthMeServer).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      email: "admin@example.com",
      isTenantAdmin: true
    });

    const result = await AdminLayout({ children: <div data-testid="admin-child">content</div> });

    expect(redirect).not.toHaveBeenCalled();
    expect(result).toBeTruthy();
  });
});
