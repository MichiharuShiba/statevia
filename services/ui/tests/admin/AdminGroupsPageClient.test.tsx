import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { AdminGroupsPageClient } from "../../app/admin/AdminGroupsPageClient";
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
    apiPost: vi.fn()
  };
});

import { apiGet, apiPost } from "../../app/lib/api";

const sampleGroup = {
  groupId: "group-1",
  name: "Operators",
  isSystem: false,
  memberCount: 2,
  permissionCount: 1,
  updatedAt: "2026-01-01T00:00:00Z"
};

describe("AdminGroupsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockClear();
    vi.mocked(apiPost).mockClear();
    vi.mocked(apiGet).mockResolvedValue([sampleGroup]);
    vi.mocked(apiPost).mockResolvedValue({ ...sampleGroup, groupId: "group-2", name: "Support" });
  });

  it("グループ一覧を読み込んで表示する", async () => {
    renderWithUiText(<AdminGroupsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/admin/groups");
    });
    expect(await screen.findByText("Operators")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "編集" })).toHaveAttribute("href", "/admin/groups/group-1");
  });

  it("システムグループにはバッジを表示する", async () => {
    vi.mocked(apiGet).mockResolvedValue([{ ...sampleGroup, isSystem: true }]);

    renderWithUiText(<AdminGroupsPageClient />);

    expect(await screen.findByText("システム")).toBeInTheDocument();
  });

  it("グループを作成して一覧を再読み込みする", async () => {
    renderWithUiText(<AdminGroupsPageClient />);
    await screen.findByText("Operators");

    fireEvent.change(screen.getByLabelText("グループ名"), { target: { value: "Support" } });
    fireEvent.click(screen.getByRole("button", { name: "作成" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/admin/groups", { name: "Support" });
    });
    expect(apiGet).toHaveBeenCalledTimes(2);
  });

  it("一覧取得失敗時にエラー状態を表示する", async () => {
    vi.mocked(apiGet).mockRejectedValue(new Error("network"));

    renderWithUiText(<AdminGroupsPageClient />);

    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.pageState.error);
  });
});
