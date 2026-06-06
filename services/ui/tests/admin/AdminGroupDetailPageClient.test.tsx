import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { AdminGroupDetailPageClient } from "../../app/admin/groups/[groupId]/AdminGroupDetailPageClient";
import { renderWithUiText } from "../testUtils";

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

import { apiGet } from "../../app/lib/api";

describe("AdminGroupDetailPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/groups/group-1") {
        return {
          groupId: "group-1",
          name: "Operators",
          isSystem: false,
          memberUserIds: ["user-1"],
          permissionKeys: ["definitions.read"]
        };
      }
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
          }
        ];
      }
      throw new Error(`unexpected path ${path}`);
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
  });
});
