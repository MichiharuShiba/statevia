import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import DefinitionDetailPage from "../../app/definitions/[definitionId]/page";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "../../app/lib/api";

describe("DefinitionDetailPage", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Demo",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });
  });

  it("サーバーページ経由で詳細を表示する", async () => {
    const page = await DefinitionDetailPage({ params: Promise.resolve({ definitionId: "def-1" }) });
    renderWithUiText(page);

    await waitFor(() => {
      expect(screen.getByText("Demo")).toBeInTheDocument();
    });
  });
});
