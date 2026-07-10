import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { DefinitionDetailClient } from "../../app/definitions/[definitionId]/DefinitionDetailClient";
import { renderWithUiText } from "../testUtils";

const pushMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn(), apiDelete: vi.fn() };
});

import { apiDelete, apiGet } from "../../app/lib/api";

describe("DefinitionDetailClient", () => {
  beforeEach(() => {
    pushMock.mockReset();
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiGet).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Demo",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });
    vi.mocked(apiDelete).mockResolvedValue(undefined);
  });

  it("定義詳細を読み込んで表示する", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });
    expect(apiGet).toHaveBeenCalledWith("/definitions/def-1");
  });

  it("削除確認後に apiDelete を呼び一覧へ遷移する", async () => {
    // Arrange
    renderWithUiText(<DefinitionDetailClient definitionId="def-1" />);
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "定義を削除" }));
    fireEvent.click(screen.getByRole("button", { name: "削除する" }));

    // Assert
    await waitFor(() => {
      expect(apiDelete).toHaveBeenCalledWith("/definitions/def-1");
    });
    expect(pushMock).toHaveBeenCalledWith("/definitions");
  });
});
