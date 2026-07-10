import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { DefinitionsPageClient } from "../../app/definitions/DefinitionsPageClient";
import { renderWithUiText } from "../testUtils";

const replaceMock = vi.fn();
const pushMock = vi.fn();
let searchParams = new URLSearchParams("limit=20&offset=0");

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock, replace: replaceMock }),
  useSearchParams: () => searchParams
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    apiDelete: vi.fn(),
    apiPost: vi.fn()
  };
});

import { apiDelete, apiGet, apiPost, buildDefinitionsListPath } from "../../app/lib/api";

describe("DefinitionsPageClient", () => {
  beforeEach(() => {
    searchParams = new URLSearchParams("limit=20&offset=0");
    replaceMock.mockReset();
    pushMock.mockReset();
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiDelete).mockReset();
    vi.mocked(apiPost).mockReset();
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
    vi.mocked(apiDelete).mockResolvedValue(undefined);
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Demo",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });
  });

  it("定義一覧を読み込んで空状態を表示する", async () => {
    // Arrange / Act
    renderWithUiText(<DefinitionsPageClient />);

    // Assert
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(apiGet).toHaveBeenCalledWith(
      "/definitions?limit=20&offset=0&sortBy=createdAt&sortOrder=desc"
    );
  });

  it("削除済みを含むトグルで includeDeleted=true の URL に置換する", async () => {
    // Arrange
    renderWithUiText(<DefinitionsPageClient />);
    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });

    // Act
    fireEvent.click(screen.getByLabelText("削除済みを含む"));

    // Assert
    expect(replaceMock).toHaveBeenCalledWith(
      buildDefinitionsListPath({
        pagination: { limit: 20, offset: 0 },
        sort: { sortBy: "createdAt", sortOrder: "desc" },
        includeDeleted: true
      }),
      { scroll: false }
    );
  });

  it("active 行の削除確認後に apiDelete を呼ぶ", async () => {
    // Arrange
    vi.mocked(apiGet).mockResolvedValue({
      items: [
        {
          displayId: "def-1",
          resourceId: "res-1",
          name: "Demo",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z"
        }
      ],
      totalCount: 1
    });
    renderWithUiText(<DefinitionsPageClient />);
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "削除" }));
    fireEvent.click(screen.getByRole("button", { name: "削除する" }));

    // Assert
    await waitFor(() => {
      expect(apiDelete).toHaveBeenCalledWith("/definitions/def-1");
    });
  });

  it("削除済み行の復元確認後に apiPost restore を呼ぶ", async () => {
    // Arrange
    searchParams = new URLSearchParams("limit=20&offset=0&includeDeleted=true");
    vi.mocked(apiGet).mockResolvedValue({
      items: [
        {
          displayId: "def-1",
          resourceId: "res-1",
          name: "Demo",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
          deletedAt: "2026-07-01T00:00:00Z"
        }
      ],
      totalCount: 1
    });
    renderWithUiText(<DefinitionsPageClient />);
    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });

    // Act
    fireEvent.click(screen.getByRole("button", { name: "復元" }));
    fireEvent.click(screen.getByRole("button", { name: "復元する" }));

    // Assert
    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/definitions/def-1/restore", {});
    });
  });
});
