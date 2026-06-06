import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { AdminUsersPageClient } from "../../app/admin/users/AdminUsersPageClient";
import { renderWithUiText } from "../testUtils";

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPost: vi.fn(),
    apiPatch: vi.fn()
  };
});

import { apiGet } from "../../app/lib/api";

describe("AdminUsersPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue([
      {
        userId: "user-1",
        principalId: "principal-1",
        email: "admin@example.com",
        displayName: "Admin",
        isTenantAdmin: true,
        isActive: true,
        groupIds: [],
        createdAt: "2026-01-01T00:00:00Z"
      }
    ]);
  });

  it("ユーザー一覧を読み込んで表示する", async () => {
    renderWithUiText(<AdminUsersPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/users");
    });
    expect(await screen.findByText("admin@example.com")).toBeInTheDocument();
  });
});
