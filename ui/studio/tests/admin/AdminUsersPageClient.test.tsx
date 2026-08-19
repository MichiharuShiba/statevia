import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { AdminUsersPageClient } from "@/features/admin/ui/AdminUsersPageClient";
import { renderWithUiText } from "../testUtils";
import { uiText } from "@/shared/i18n/uiText";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn(),
    apiPatch: vi.fn(),
    apiPut: vi.fn()
  };
});

import { apiGet, apiPatch, apiPost, apiPut } from "@/shared/api";

const sampleUser = {
  userId: "user-1",
  principalId: "principal-1",
  username: "admin",
  email: "admin@example.com",
  displayName: "Admin",
  isTenantAdmin: true,
  isActive: true,
  groupIds: [],
  createdAt: "2026-01-01T00:00:00Z"
};

describe("AdminUsersPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockClear();
    vi.mocked(apiPost).mockClear();
    vi.mocked(apiPatch).mockClear();
    vi.mocked(apiPut).mockClear();
    vi.mocked(apiGet).mockResolvedValue([sampleUser]);
    vi.mocked(apiPost).mockResolvedValue(sampleUser);
    vi.mocked(apiPatch).mockResolvedValue({ ...sampleUser, isActive: false });
    vi.mocked(apiPut).mockResolvedValue(null);
  });

  it("ユーザー一覧を読み込んで表示する", async () => {
    renderWithUiText(<AdminUsersPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/users");
    });
    expect(await screen.findByText("admin")).toBeInTheDocument();
  });

  it("ユーザーを作成して一覧を再読み込みする", async () => {
    renderWithUiText(<AdminUsersPageClient />);
    await screen.findByText("admin");

    fireEvent.change(screen.getByLabelText("ユーザー名"), {
      target: { value: "new-user" }
    });
    fireEvent.change(screen.getByLabelText("メールアドレス（任意）"), {
      target: { value: "new@example.com" }
    });
    fireEvent.change(screen.getByLabelText("初期パスワード"), {
      target: { value: "password123" }
    });
    expect(screen.getByLabelText("初期パスワード")).toHaveAttribute("minLength", "8");
    fireEvent.change(screen.getByLabelText("表示名（任意）"), {
      target: { value: "New User" }
    });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/admin/users", {
        username: "new-user",
        email: "new@example.com",
        password: "password123",
        displayName: "New User",
        isTenantAdmin: false
      });
    });
    expect(apiGet).toHaveBeenCalledTimes(2);
  });

  it("有効ユーザーを無効化する", async () => {
    renderWithUiText(<AdminUsersPageClient />);
    await screen.findByText("admin");

    fireEvent.click(screen.getByRole("button", { name: "無効化" }));

    await waitFor(() => {
      expect(apiPatch).toHaveBeenCalledWith("/admin/users/user-1", { isActive: false });
    });
    expect(apiGet).toHaveBeenCalledTimes(2);
  });

  it("一覧取得失敗時にエラー状態を表示する", async () => {
    vi.mocked(apiGet).mockRejectedValue(new Error("network"));

    renderWithUiText(<AdminUsersPageClient />);

    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.pageState.error);
  });

  it("パスワード更新ダイアログで一致時のみ PUT する", async () => {
    renderWithUiText(<AdminUsersPageClient />);
    await screen.findByText("admin");

    fireEvent.click(screen.getByRole("button", { name: uiText.admin.users.updatePassword }));
    expect(screen.getByRole("dialog").tagName).toBe("DIALOG");
    fireEvent.change(screen.getByLabelText(uiText.admin.users.newPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.change(screen.getByLabelText(uiText.admin.users.confirmPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.click(screen.getByRole("button", { name: uiText.admin.users.updatePasswordSubmit }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/users/user-1/password", { newPassword: "newsecret1" });
    });
  });

  it("確認パスワード不一致では PUT しない", async () => {
    renderWithUiText(<AdminUsersPageClient />);
    await screen.findByText("admin");

    fireEvent.click(screen.getByRole("button", { name: uiText.admin.users.updatePassword }));
    fireEvent.change(screen.getByLabelText(uiText.admin.users.newPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.change(screen.getByLabelText(uiText.admin.users.confirmPasswordLabel), {
      target: { value: "othersecret" }
    });
    fireEvent.click(screen.getByRole("button", { name: uiText.admin.users.updatePasswordSubmit }));

    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.admin.users.passwordMismatch);
    expect(apiPut).not.toHaveBeenCalled();
  });
});
