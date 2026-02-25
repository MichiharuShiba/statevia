import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { TenantMissingBanner } from "../../../app/components/execution/TenantMissingBanner";
import * as api from "../../../app/lib/api";

vi.mock("../../../app/lib/api", () => ({
  getApiConfig: vi.fn()
}));

describe("TenantMissingBanner", () => {
  it("テナント未指定のときバナーを表示する", () => {
    // Arrange
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "", authToken: "" });

    // Act
    render(<TenantMissingBanner />);

    // Assert
    expect(screen.getByRole("alert")).toHaveTextContent("テナントが未指定です");
    expect(screen.getByText(/NEXT_PUBLIC_TENANT_ID/)).toBeInTheDocument();
    expect(screen.getByText(/CORE_API_TENANT_ID/)).toBeInTheDocument();
  });

  it("テナント指定時は何も表示しない", () => {
    // Arrange
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "tenant-1", authToken: "" });

    // Act
    const { container } = render(<TenantMissingBanner />);

    // Assert
    expect(container.firstChild).toBeNull();
  });
});
