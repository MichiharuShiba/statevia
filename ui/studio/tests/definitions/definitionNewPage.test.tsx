import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import DefinitionNewPage from "../../app/definitions/new/page";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "../../app/lib/api";

describe("DefinitionNewPage", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({
      schemaVersion: "1",
      nodesVersion: 1,
      schema: { properties: {} }
    });
  });

  it("新規作成ページを描画する", async () => {
    renderWithUiText(<DefinitionNewPage />);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "保存" })).toBeInTheDocument();
    });
  });
});
