import { act, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { TenantMissingBanner } from "../../../app/components/execution/TenantMissingBanner";
import * as api from "../../../app/lib/api";
import { uiText } from "../../../app/lib/uiText";

vi.mock("../../../app/lib/api", () => ({
  getApiConfig: vi.fn()
}));

describe("TenantMissingBanner", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ authenticated: false, tenantKey: "" })
        } as Response)
      )
    );
  });

  it("セッション確認中はバナーを表示しない", async () => {
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "", authToken: "" });
    let resolveFetch: (value: Response) => void = () => {};
    vi.stubGlobal(
      "fetch",
      vi.fn(
        () =>
          new Promise<Response>((resolve) => {
            resolveFetch = resolve;
          })
      )
    );

    const { container } = render(<TenantMissingBanner />);
    expect(container.firstChild).toBeNull();

    await act(async () => {
      resolveFetch({
        ok: true,
        json: () => Promise.resolve({ authenticated: false, tenantKey: "" })
      } as Response);
      await Promise.resolve();
    });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toBeInTheDocument();
    });
  });

  it("テナント未指定かつ未ログインのときバナーを表示する", async () => {
    // Arrange
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "", authToken: "" });

    // Act
    render(<TenantMissingBanner />);
    const noticeParts = uiText.tenantMissingBanner.noticeParts(
      uiText.actions.load,
      uiText.actions.cancel,
      uiText.actions.resume
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(noticeParts.beforePrimaryEnv.trim());
    });
    expect(screen.getByText(/NEXT_PUBLIC_TENANT_ID/)).toBeInTheDocument();
    expect(screen.getByText(/\/login/)).toBeInTheDocument();
  });

  it("テナント指定時は何も表示しない", () => {
    // Arrange
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "tenant-1", authToken: "" });

    // Act
    const { container } = render(<TenantMissingBanner />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("Cookie セッション時は何も表示しない", async () => {
    // Arrange
    vi.mocked(api.getApiConfig).mockReturnValue({ tenantId: "", authToken: "" });
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ authenticated: true, tenantKey: "default" })
        } as Response)
      )
    );

    // Act
    const { container } = render(<TenantMissingBanner />);

    // Assert
    await waitFor(() => {
      expect(container.firstChild).toBeNull();
    });
  });
});
