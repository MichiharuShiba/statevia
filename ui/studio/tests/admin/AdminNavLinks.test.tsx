import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { AdminNavLinks } from "../../app/components/layout/AdminNavLinks";
import { renderWithUiText } from "../testUtils";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  )
}));

vi.mock("../../app/lib/fetchAuthMe", () => ({
  fetchAuthMe: vi.fn()
}));

import { fetchAuthMe } from "../../app/lib/fetchAuthMe";

describe("AdminNavLinks", () => {
  beforeEach(() => {
    vi.mocked(fetchAuthMe).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      email: "admin@example.com",
      isTenantAdmin: true
    });
  });

  it("テナント管理者には管理ナビを表示する", async () => {
    renderWithUiText(<AdminNavLinks />);

    expect(await screen.findByRole("link", { name: "ユーザー管理" })).toHaveAttribute("href", "/admin/users");
    expect(screen.getByRole("link", { name: "グループ管理" })).toHaveAttribute("href", "/admin/groups");
    expect(screen.getByRole("link", { name: "API キー" })).toHaveAttribute("href", "/admin/api-keys");
  });

  it("非管理者には管理ナビを表示しない", async () => {
    vi.mocked(fetchAuthMe).mockResolvedValue({
      tenantId: "tenant",
      tenantKey: "default",
      principalId: "principal",
      email: "member@example.com",
      isTenantAdmin: false
    });

    renderWithUiText(<AdminNavLinks />);

    await waitFor(() => {
      expect(fetchAuthMe).toHaveBeenCalled();
    });
    expect(screen.queryByRole("link", { name: "ユーザー管理" })).not.toBeInTheDocument();
  });
});
