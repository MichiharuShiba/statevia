import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { AdminGroupDetailPageClient } from "../../app/admin/groups/[groupId]/AdminGroupDetailPageClient";
import { renderWithUiText } from "../testUtils";
import { uiText } from "../../app/lib/uiText";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  )
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiPut: vi.fn()
  };
});

import { apiGet, apiPut } from "../../app/lib/api";

const groupDetail = {
  groupId: "group-1",
  name: "Operators",
  isSystem: false,
  memberUserIds: ["user-1"],
  permissionKeys: ["definitions.read"]
};

describe("AdminGroupDetailPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/groups/group-1") return groupDetail;
      if (path === "/admin/users") {
        return [
          {
            userId: "user-1",
            principalId: "principal-1",
            email: "member@example.com",
            displayName: "Member",
            isTenantAdmin: false,
            isActive: true,
            groupIds: ["group-1"],
            createdAt: "2026-01-01T00:00:00Z"
          },
          {
            userId: "user-2",
            principalId: "principal-2",
            email: "inactive@example.com",
            displayName: "Inactive",
            isTenantAdmin: false,
            isActive: false,
            groupIds: [],
            createdAt: "2026-01-01T00:00:00Z"
          }
        ];
      }
      if (path === "/admin/permissions") {
        return [
          {
            permissionKey: "definitions.read",
            displayLabel: "Read definitions",
            displayKey: "permissions.definitionsRead",
            isSystem: true,
            isDeprecated: false
          },
          {
            permissionKey: "tenant.admin",
            displayLabel: "Tenant admin",
            displayKey: "permissions.tenantAdmin",
            isSystem: true,
            isDeprecated: false
          }
        ];
      }
      throw new Error(`unexpected path ${path}`);
    });
    vi.mocked(apiPut).mockImplementation(async (path: string, body: unknown) => {
      if (path === "/admin/groups/group-1/members") {
        const userIds = (body as { userIds: string[] }).userIds;
        return { ...groupDetail, memberUserIds: userIds };
      }
      if (path === "/admin/groups/group-1/permissions") {
        const permissionKeys = (body as { permissionKeys: string[] }).permissionKeys;
        return { ...groupDetail, permissionKeys };
      }
      throw new Error(`unexpected put ${path}`);
    });
  });

  it("グループ詳細を読み込んでメンバーと権限を表示する", async () => {
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/groups/group-1");
    });
    expect(await screen.findByRole("heading", { name: "Operators" })).toBeInTheDocument();
    expect(screen.getByText("member@example.com")).toBeInTheDocument();
    expect(screen.getByLabelText("Read definitions (definitions.read)")).toBeInTheDocument();
    expect(screen.queryByLabelText(/tenant\.admin/)).toBeNull();
  });

  it("無効ユーザーには inactive 表記を付ける", async () => {
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    expect(
      await screen.findByText(`inactive@example.com (${uiText.admin.users.inactive})`)
    ).toBeInTheDocument();
  });

  it("メンバー変更を保存する", async () => {
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });

    fireEvent.click(screen.getByLabelText(`inactive@example.com (${uiText.admin.users.inactive})`));
    fireEvent.click(screen.getByRole("button", { name: "メンバーを保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/groups/group-1/members", {
        userIds: ["user-1", "user-2"]
      });
    });
  });

  it("権限変更を保存する", async () => {
    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);
    await screen.findByRole("heading", { name: "Operators" });

    fireEvent.click(screen.getByLabelText("Read definitions (definitions.read)"));
    fireEvent.click(screen.getByRole("button", { name: "権限を保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/admin/groups/group-1/permissions", {
        permissionKeys: []
      });
    });
  });

  it("取得失敗時にエラー状態を表示する", async () => {
    vi.mocked(apiGet).mockRejectedValue(new Error("network"));

    renderWithUiText(<AdminGroupDetailPageClient groupId="group-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.pageState.error);
  });
});
