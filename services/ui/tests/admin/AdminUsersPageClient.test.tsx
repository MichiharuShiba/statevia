import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { AdminUsersPageClient } from "../../app/admin/users/AdminUsersPageClient";
import { renderWithUiText } from "../testUtils";
import { uiText } from "../../app/lib/uiText";

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn(),
    apiPatch: vi.fn()
  };
});

import { apiGet, apiPatch, apiPost } from "../../app/lib/api";

const sampleUser = {
  userId: "user-1",
  principalId: "principal-1",
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
    vi.mocked(apiGet).mockResolvedValue([sampleUser]);
    vi.mocked(apiPost).mockResolvedValue(sampleUser);
    vi.mocked(apiPatch).mockResolvedValue({ ...sampleUser, isActive: false });
  });

  it("ユーザー一覧を読み込んで表示する", async () => {
    renderWithUiText(<AdminUsersPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/users");
    });
    expect(await screen.findByText("admin@example.com")).toBeInTheDocument();
  });

  it("ユーザーを作成して一覧を再読み込みする", async () => {
    renderWithUiText(<AdminUsersPageClient />);
    await screen.findByText("admin@example.com");

    fireEvent.change(screen.getByLabelText("メールアドレス"), {
      target: { value: "new@example.com" }
    });
    fireEvent.change(screen.getByLabelText("初期パスワード"), {
      target: { value: "password123" }
    });
    fireEvent.change(screen.getByLabelText("表示名（任意）"), {
      target: { value: "New User" }
    });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/admin/users", {
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
    await screen.findByText("admin@example.com");

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
});
