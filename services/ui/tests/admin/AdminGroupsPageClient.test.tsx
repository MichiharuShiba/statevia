import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { AdminGroupsPageClient } from "../../app/admin/AdminGroupsPageClient";
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
    apiPost: vi.fn()
  };
});

import { apiGet } from "../../app/lib/api";

describe("AdminGroupsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue([
      {
        groupId: "group-1",
        name: "Operators",
        isSystem: false,
        memberCount: 2,
        permissionCount: 1,
        updatedAt: "2026-01-01T00:00:00Z"
      }
    ]);
  });

  it("グループ一覧を読み込んで表示する", async () => {
    renderWithUiText(<AdminGroupsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/groups");
    });
    expect(await screen.findByText("Operators")).toBeInTheDocument();
  });
});
