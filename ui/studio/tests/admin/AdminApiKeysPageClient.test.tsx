import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { AdminApiKeysPageClient } from "../../app/admin/api-keys/AdminApiKeysPageClient";
import * as api from "../../app/lib/api";

vi.mock("../../app/lib/api", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiDelete: vi.fn()
}));

describe("AdminApiKeysPageClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("一覧を読み込み発行フォームを表示する", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/api-keys") {
        return [
          {
            apiKeyId: "key-1",
            name: "CI Runner",
            keyPrefix: "stv_abcd",
            allowedScopes: ["executions.read"],
            expiresAt: null,
            lastUsedAt: null,
            createdAt: "2026-06-01T00:00:00Z",
            isActive: true
          }
        ];
      }
      if (path === "/admin/permissions") {
        return [
          {
            permissionKey: "executions.read",
            displayLabel: "Read executions",
            displayKey: "permissions.executionsRead",
            isSystem: true,
            isDeprecated: false
          }
        ];
      }
      throw new Error(`unexpected path: ${path}`);
    });

    // Act
    render(<AdminApiKeysPageClient />);

    // Assert
    await waitFor(() => {
      expect(api.apiGet).toHaveBeenCalledWith("/admin/api-keys");
    });
    expect(await screen.findByText("CI Runner")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "API キーを発行" })).toBeInTheDocument();
  });

  it("API キーを発行すると平文キーを一度だけ表示する", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/api-keys") return [];
      if (path === "/admin/permissions") {
        return [
          {
            permissionKey: "executions.read",
            displayLabel: "Read executions",
            displayKey: "permissions.executionsRead",
            isSystem: true,
            isDeprecated: false
          }
        ];
      }
      throw new Error(`unexpected path: ${path}`);
    });
    vi.mocked(api.apiPost).mockResolvedValue({
      apiKeyId: "key-new",
      name: "New Key",
      keyPrefix: "stv_test",
      plainKey: "stv_plain_secret_value",
      allowedScopes: ["executions.read"],
      expiresAt: null,
      createdAt: "2026-06-06T00:00:00Z"
    });

    render(<AdminApiKeysPageClient />);
    await screen.findByRole("heading", { name: "API キーを発行" });

    // Act
    fireEvent.change(screen.getByLabelText("表示名"), { target: { value: "New Key" } });
    fireEvent.click(screen.getByRole("checkbox"));
    fireEvent.click(screen.getByRole("button", { name: "発行" }));

    // Assert
    await waitFor(() => {
      expect(api.apiPost).toHaveBeenCalledWith("/admin/api-keys", {
        name: "New Key",
        allowedScopes: ["executions.read"]
      });
    });
    expect(await screen.findByDisplayValue("stv_plain_secret_value")).toBeInTheDocument();
  });

  it("有効なキーを失効できる", async () => {
    // Arrange
    vi.mocked(api.apiGet).mockImplementation(async (path: string) => {
      if (path === "/admin/api-keys") {
        return [
          {
            apiKeyId: "key-1",
            name: "CI Runner",
            keyPrefix: "stv_abcd",
            allowedScopes: ["executions.read"],
            expiresAt: null,
            lastUsedAt: null,
            createdAt: "2026-06-01T00:00:00Z",
            isActive: true
          }
        ];
      }
      if (path === "/admin/permissions") return [];
      throw new Error(`unexpected path: ${path}`);
    });
    vi.mocked(api.apiDelete).mockResolvedValue(undefined);

    render(<AdminApiKeysPageClient />);
    await screen.findByText("CI Runner");

    // Act
    fireEvent.click(screen.getByRole("button", { name: "失効" }));

    // Assert
    await waitFor(() => {
      expect(api.apiDelete).toHaveBeenCalledWith("/admin/api-keys/key-1");
    });
  });
});
