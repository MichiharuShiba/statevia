import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import DefinitionRunStartPage from "../../app/definitions/[definitionId]/run/page";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useParams: () => ({ definitionId: "def-1" }),
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiPost: vi.fn() };
});

import { apiPost } from "../../app/lib/api";

describe("DefinitionRunStartPage", () => {
  beforeEach(() => {
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "ex-new",
      resourceId: "res-new",
      graphId: "g-1",
      status: "Running",
      startedAt: "2026-01-01T00:00:00Z",
      cancelRequested: false,
      restartLost: false
    });
  });

  it("ワークフロー開始ボタンで API を呼ぶ", async () => {
    renderWithUiText(<DefinitionRunStartPage />);

    fireEvent.click(screen.getByRole("button", { name: "ワークフロー開始" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/workflows", { definitionId: "def-1" });
    });
  });
});
